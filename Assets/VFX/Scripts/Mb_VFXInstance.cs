// Mb_VFXInstance.cs
// Attach this to every VFX prefab that will be managed by Mb_VFXManager.
// This component is the "brain" of one pooled VFX object — it knows how to
// play itself, stop itself, and return itself to the pool when it's done.
//
// LIFECYCLE OF ONE VFX PLAY:
//   1. Mb_VFXManager fetches an inactive instance from its pool.
//   2. Mb_VFXManager calls Play() — positions the object, optionally parents it,
//      activates the GameObject, and starts the return-to-pool timer.
//   3. The ParticleSystem plays. The return timer counts down.
//   4a. Timer hits zero → ReturnToPool() is called automatically.
//   4b. OR: Stop() is called externally (e.g. a status effect was removed early)
//       → particles stop and ReturnToPool() is called immediately.
//
// POOL SAFETY:
//   ReturnToPool() always unparents before deactivating. This is critical for
//   status VFX that are parented to CuBots — if the CuBot is deactivated first,
//   the VFX would be deactivated with it. Mb_StatusVFXHandler.OnDisable() calls
//   Stop() to handle this, but unparenting here is a second safety net.
//
// PAUSE HANDLING:
//   Subscribes to Mb_PauseManager events in OnEnable/OnDisable.
//   Pauses and resumes the ParticleSystem directly so VFX freeze with the game.
//   The return-to-pool coroutine uses WaitForSeconds which already freezes
//   when Time.timeScale = 0, so no extra handling is needed for the timer.
//
// INSPECTOR SETUP:
//   - Attach this component to your particle system prefab.
//   - The ParticleSystem component must be on the same GameObject (or assigned
//     in the Inspector via the _ParticleSystem field).
//   - Do NOT set "Stop Action" on the ParticleSystem to "Destroy" —
//     this system manages lifetime itself. Set it to "None" or "Disable".

using System.Collections;
using UnityEngine;

public class Mb_VFXInstance : MonoBehaviour
{
    #region Inspector Fields        //----------------------------------------

    [Header("References")]
    [Tooltip("The ParticleSystem this instance controls. " +
             "Leave null to auto-find on this same GameObject.")]
    [SerializeField] private ParticleSystem _ParticleSystem;

    #endregion                      //----------------------------------------


    #region Private State           //----------------------------------------

    // Which VFXType this instance represents — set by Mb_VFXManager when the
    // pool is built. Used when returning to the pool so the manager knows
    // which pool to return it to.
    private VFXType _vfxType;

    // Handle to the active return-to-pool coroutine.
    // Stored so we can cancel it if Stop() is called before the timer expires.
    private Coroutine _returnCoroutine;

    // Tracks whether this instance is currently in use.
    // Prevents double-returns if Stop() is called while a return is already queued.
    private bool _isPlaying = false;

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        // Auto-find the ParticleSystem if one wasn't assigned in the Inspector
        if (_ParticleSystem == null)
            _ParticleSystem = GetComponent<ParticleSystem>();

        if (_ParticleSystem == null)
            Debug.LogError($"[Mb_VFXInstance] No ParticleSystem found on {gameObject.name}. " +
                           "Attach a ParticleSystem component or assign it in the Inspector.");
    }


    private void OnEnable()
    {
        // Subscribe to pause events so this instance freezes with the game.
        // We subscribe here (not in Awake) so the subscription is active only
        // while this instance is active in the scene — pool-safe.
        Mb_PauseManager.OnPaused += HandlePause;
        Mb_PauseManager.OnResumed += HandleResume;
    }


    private void OnDisable()
    {
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;

        // If this object was deactivated externally (e.g. its parent CuBot was pooled)
        // before Stop() was called, clean up state so this instance is safe to reuse.
        // We don't call ReturnToPool() here because the object is already inactive —
        // we just need to reset the playing flag and stop the coroutine reference.
        _isPlaying = false;
        _returnCoroutine = null;
    }

    #endregion                      //----------------------------------------


    #region Public API              //----------------------------------------

    /// <summary>
    /// Called by Mb_VFXManager when it assigns this type to a pool slot.
    /// Must be called once before Play() is ever used.
    /// </summary>
    public void Initialize(VFXType type)
    {
        _vfxType = type;
    }


    /// <summary>
    /// Positions this instance, activates it, and begins playback.
    /// Call this immediately after fetching an instance from the pool.
    ///
    /// parent: pass a Transform to attach this VFX to a character (status effects).
    ///         Pass null for world-space one-shots (hit sparks, ability casts).
    /// lifetime: how many seconds before auto-returning to the pool.
    ///           Sourced from VFXEntry.Lifetime in SO_VFXLibrary.
    /// </summary>
    public void Play(Vector3 position, Quaternion rotation, float lifetime, Transform parent = null)
    {
        // Safety: if somehow called while already playing, stop cleanly first
        if (_isPlaying)
            StopImmediate();

        if (parent != null)
        {
            // worldPositionStays: false means the instance snaps to the parent's
            // local origin with zero offset. We then zero out local position and
            // rotation so the VFX sits exactly at the parent pivot with no drift.
            transform.SetParent(parent, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            // No parent — world-space one-shot. Set parent to null first to clear
            // any previous parent, then apply the world position and rotation.
            transform.SetParent(null);
            transform.SetPositionAndRotation(position, rotation);
        }

        gameObject.SetActive(true);

        // Play the particle system from the beginning.
        // withChildren: true ensures sub-emitters (e.g. trail particles) also play.
        if (_ParticleSystem != null)
        {
            _ParticleSystem.Clear();
            _ParticleSystem.Play(withChildren: true);
        }

        _isPlaying = true;

        // Start the return-to-pool countdown.
        // WaitForSeconds respects Time.timeScale = 0 (pause), so the timer
        // naturally freezes when the game is paused — no extra handling needed.
        _returnCoroutine = StartCoroutine(ReturnAfterLifetime(lifetime));
    }


    /// <summary>
    /// Stops this VFX immediately and returns it to the pool.
    /// Use this when a status effect is removed before its natural duration ends,
    /// or when an effect needs to be cancelled mid-play for any reason.
    /// Safe to call even if this instance is not currently playing.
    /// </summary>
    public void Stop()
    {
        if (!_isPlaying) return;

        StopImmediate();
        ReturnToPool();
    }

    #endregion                      //----------------------------------------


    #region Internal Helpers        //----------------------------------------

    // Stops particles and cancels the return coroutine without returning to pool.
    // Called by both Stop() and before re-playing a still-active instance.
    private void StopImmediate()
    {
        // Cancel the pending return coroutine — we are handling the return ourselves
        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }

        // Stop the particle system and all child emitters immediately.
        // withChildren: true so sub-emitters (trails, bursts) also stop.
        if (_ParticleSystem != null)
            _ParticleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);

        _isPlaying = false;
    }


    // Unparents and deactivates this instance, returning it to the pool.
    // ALWAYS unparent first — if this was a child of a CuBot that is about to be
    // deactivated, unparenting here prevents the VFX from being deactivated with it.
    private void ReturnToPool()
    {
        // Unparent so we don't get swept up in a parent's deactivation
        transform.SetParent(null);

        // Deactivate — Mb_VFXManager recognises inactive objects as available pool slots
        gameObject.SetActive(false);

        // Notify the manager that this instance is back in circulation.
        // The manager adds it back to the available stack for its VFXType pool.
        Mb_VFXManager.ReturnInstance(_vfxType, this);
    }


    // Waits for the configured lifetime, then returns to the pool.
    // The coroutine is cancelled by StopImmediate() if Stop() is called early.
    private IEnumerator ReturnAfterLifetime(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        // Lifetime elapsed naturally — stop particles and return cleanly
        StopImmediate();
        ReturnToPool();
    }

    #endregion                      //----------------------------------------


    #region Pause Handling          //----------------------------------------

    private void HandlePause()
    {
        // Pause the particle system so effects freeze mid-animation with the game.
        // The return-to-pool coroutine (WaitForSeconds) already freezes automatically
        // when Time.timeScale = 0, so no coroutine handling is needed here.
        if (_ParticleSystem != null && _isPlaying)
            _ParticleSystem.Pause(withChildren: true);
    }


    private void HandleResume()
    {
        // Resume playback from where it was paused.
        // Guard: only resume if we were actually playing — don't accidentally start
        // a particle system that wasn't running before the pause.
        if (_ParticleSystem != null && _isPlaying)
            _ParticleSystem.Play(withChildren: true);
    }

    #endregion                      //----------------------------------------
}