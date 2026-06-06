// Mb_StatusVFXHandler.cs
// Manages looping status effect VFX on a character — both the body-attached
// particle effect and the camera vignette/overlay effect.
//
// TWO VFX LAYERS PER STATUS:
//   1. Character VFX — parented to this character's transform, follows movement.
//      Spawned using the character's world position + this transform as parent.
//   2. Camera VFX   — parented to the Cinemachine brain camera, stays screen-space.
//      Spawned using the camera's position + camera transform as parent.
//      Only spawns for the Guardian (player character) — CuBots do not have a
//      personal camera vignette. Checked via the _isCameraVFXEnabled flag.
//
// CINEMACHINE NOTE:
//   Cinemachine moves the real camera in LateUpdate. Parenting the VFX to
//   cam.transform means it follows automatically — no per-frame position updates
//   needed. The one-frame position offset on spawn is negligible for vignettes
//   because vignettes are typically centered on screen and do not have a
//   meaningful world position anyway. For a true screen-space overlay, consider
//   using a UI canvas element instead (see TODO below).
//
// CAMERA VFX LIFETIME:
//   Set Lifetime to 999 in SO_VFXLibrary for all camera vignette entries,
//   exactly like character status VFX. Mb_StatusVFXHandler calls Stop()
//   explicitly when the status ends — the timer should never fire.
//
// POOL SAFETY:
//   OnDisable() stops ALL active VFX (both character and camera layers) before
//   the character GameObject deactivates. This prevents orphaned camera VFX
//   from persisting on screen after a CuBot returns to the pool.
//   For the Guardian this is less critical (Guardian rarely deactivates mid-play)
//   but the same cleanup runs for consistency.
//
// INSPECTOR SETUP:
//   - No fields required for character VFX — transform is fetched automatically.
//   - Enable Camera VFX: tick this checkbox ONLY on the Guardian prefab.
//     Leave it unticked on all CuBot prefabs.
//   - Mb_StatusEffectController must be on the same GameObject.
//   - Do NOT add any VFX code to Mb_StatusEffectController.Apply() —
//     all VFX is handled here.

using System.Collections.Generic;
using UnityEngine;

public class Mb_StatusVFXHandler : MonoBehaviour
{
    #region Inspector Fields        //----------------------------------------

    [Header("Camera VFX")]
    [Tooltip("Enable this ONLY on the Guardian prefab. " +
             "When true, status effects also spawn a vignette VFX on the camera. " +
             "Leave unticked on all CuBot prefabs — CuBots have no personal camera.")]
    [SerializeField] private bool _EnableCameraVFX = false;

    #endregion                      //----------------------------------------


    #region Private State           //----------------------------------------

    // Status controller on this same character — subscribed to in OnEnable
    private Mb_StatusEffectController _statusController;

    // Tracks which VFXType is active on this character's body per status type.
    // Key: StatusType   Value: the VFXType enum value that was spawned
    private readonly Dictionary<StatusType, VFXType> _activeCharacterVFX
        = new Dictionary<StatusType, VFXType>();

    // Tracks which VFXType is active on the camera per status type.
    // Separate dictionary from character VFX so Stop() targets the right object.
    // Only populated when _EnableCameraVFX is true.
    private readonly Dictionary<StatusType, VFXType> _activeCameraVFX
        = new Dictionary<StatusType, VFXType>();

    // Cached camera transform — resolved once on first use, not every Apply() call.
    // Avoids repeated FindGameObjectWithTag calls during combat.
    // Only populated when _EnableCameraVFX is true.
    private Transform _cameraTransform;

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        _statusController = GetComponent<Mb_StatusEffectController>();

        if (_statusController == null)
            Debug.LogError($"[Mb_StatusVFXHandler] No Mb_StatusEffectController found on " +
                           $"{gameObject.name}. Add it to the same GameObject.");

        // Cache the camera transform once at startup if camera VFX is enabled.
        // We do this in Awake (not lazily) so the first status application
        // does not incur a FindGameObjectWithTag call mid-combat.
        if (_EnableCameraVFX)
            CacheCamera();
    }


    private void OnEnable()
    {
        if (_statusController != null)
        {
            _statusController.OnStatusApplied += HandleStatusApplied;
            _statusController.OnStatusRemoved += HandleStatusRemoved;
        }
    }


    private void OnDisable()
    {
        if (_statusController != null)
        {
            _statusController.OnStatusApplied -= HandleStatusApplied;
            _statusController.OnStatusRemoved -= HandleStatusRemoved;
        }

        // Stop all active VFX on both layers before this object deactivates.
        // This covers CuBot pool returns and Guardian scene teardown.
        StopAll();
    }

    #endregion                      //----------------------------------------


    #region Event Handlers          //----------------------------------------

    private void HandleStatusApplied(StatusType statusType)
    {
        // ── CHARACTER VFX ─────────────────────────────────────────────────
        if (TryGetCharacterVFX(statusType, out VFXType characterVFX))
        {
            // If this status is somehow already tracked (rapid reapplication edge case),
            // stop the old instance first to avoid two effects running simultaneously.
            // The status controller refresh path prevents this in normal gameplay,
            // but this guard makes the VFX layer independently safe.
            if (_activeCharacterVFX.ContainsKey(statusType))
                StopCharacterVFX(statusType);

            // Parent to this character's transform so the VFX follows movement.
            // transform here is the StatusVFXHandler's own GameObject — the character root.
            Mb_VFXManager.Play(characterVFX, transform.position, transform);
            _activeCharacterVFX[statusType] = characterVFX;

            Debug.Log($"[Mb_StatusVFXHandler] Character VFX {characterVFX} started " +
                      $"for {statusType} on {gameObject.name}.");
        }

        // ── CAMERA VFX ────────────────────────────────────────────────────
        // Only runs when _EnableCameraVFX is true (Guardian only).
        if (_EnableCameraVFX && TryGetCameraVFX(statusType, out VFXType cameraVFX))
        {
            if (_cameraTransform == null)
            {
                // Try to recover the camera reference if it was lost (e.g. scene reload)
                CacheCamera();

                if (_cameraTransform == null)
                {
                    Debug.LogWarning($"[Mb_StatusVFXHandler] Camera VFX requested for " +
                                     $"{statusType} but no MainCamera was found. Skipping.");
                    return;
                }
            }

            // Stop any existing camera VFX for this status type first
            if (_activeCameraVFX.ContainsKey(statusType))
                StopCameraVFX(statusType);

            // Parent to the camera transform so the vignette follows the camera.
            // Cinemachine moves the camera in LateUpdate — parenting here means
            // the VFX automatically tracks the camera each frame with no extra code.
            // The position argument is the camera's current world position.
            // For a true screen-space overlay, replace this with a UI canvas element.
            // TODO: If the vignette needs to be pixel-perfect screen-space, use a
            //       CanvasGroup or a fullscreen UI Image instead of a world-space VFX.
            Mb_VFXManager.Play(cameraVFX, _cameraTransform.position, _cameraTransform);
            _activeCameraVFX[statusType] = cameraVFX;

            Debug.Log($"[Mb_StatusVFXHandler] Camera VFX {cameraVFX} started " +
                      $"for {statusType} on {gameObject.name}.");
        }
    }


    private void HandleStatusRemoved(StatusType statusType)
    {
        StopCharacterVFX(statusType);
        StopCameraVFX(statusType);
    }

    #endregion                      //----------------------------------------


    #region Public API              //----------------------------------------

    /// <summary>
    /// Stops all active VFX on both the character body and camera layers.
    /// Called from OnDisable() for pool safety.
    /// Can also be called from a cleanse ability alongside
    /// Mb_StatusEffectController.ClearAll().
    /// </summary>
    public void StopAll()
    {
        // Copy keys first — modifying the dictionary inside the loop is not safe
        var characterTypes = new List<StatusType>(_activeCharacterVFX.Keys);
        foreach (StatusType type in characterTypes)
            StopCharacterVFX(type);

        var cameraTypes = new List<StatusType>(_activeCameraVFX.Keys);
        foreach (StatusType type in cameraTypes)
            StopCameraVFX(type);
    }

    #endregion                      //----------------------------------------


    #region Stop Helpers            //----------------------------------------

    // Stops and unregisters the character body VFX for one status type.
    private void StopCharacterVFX(StatusType statusType)
    {
        if (!_activeCharacterVFX.TryGetValue(statusType, out VFXType vfxType))
            return;

        // gameObject here is this character — the manager uses (VFXType, GameObject)
        // as the key to find the exact instance that was parented to this character
        Mb_VFXManager.Stop(vfxType, gameObject);
        _activeCharacterVFX.Remove(statusType);
    }


    // Stops and unregisters the camera VFX for one status type.
    private void StopCameraVFX(StatusType statusType)
    {
        if (!_activeCameraVFX.TryGetValue(statusType, out VFXType vfxType))
            return;

        if (_cameraTransform != null)
            Mb_VFXManager.Stop(vfxType, _cameraTransform.gameObject);

        _activeCameraVFX.Remove(statusType);
    }

    #endregion                      //----------------------------------------


    #region Status → VFX Mappings   //----------------------------------------

    // Maps a StatusType to the VFXType that plays on the character's body.
    // Returns false if this status has no character VFX.
    private bool TryGetCharacterVFX(StatusType statusType, out VFXType vfxType)
    {
        switch (statusType)
        {
            case StatusType.Burn:
                vfxType = VFXType.Status_Burn;
                return true;

            case StatusType.Poison:
                vfxType = VFXType.Status_Poison;
                return true;

            case StatusType.MoveSlow:
            case StatusType.AttackSlow:
                // Both slow types share the same body VFX.
                // If both are active simultaneously, the first one applied wins.
                // TODO: Add separate VFXType entries if distinct visuals are needed.
                vfxType = VFXType.Status_Slow;
                return true;

            case StatusType.Stun:
                vfxType = VFXType.Status_Stun;
                return true;

            case StatusType.Root:
            case StatusType.Silence:
                // No body VFX for these CC types in the prototype.
                // TODO: Add entries once visual design is confirmed.
                vfxType = default;
                return false;

            default:
                Debug.LogWarning($"[Mb_StatusVFXHandler] No character VFX mapping for " +
                                 $"StatusType.{statusType}. Add a case to TryGetCharacterVFX().");
                vfxType = default;
                return false;
        }
    }


    // Maps a StatusType to the VFXType that plays on the camera (vignette/overlay).
    // Returns false if this status has no camera VFX.
    // Only called when _EnableCameraVFX is true — CuBots never reach this.
    private bool TryGetCameraVFX(StatusType statusType, out VFXType vfxType)
    {
        switch (statusType)
        {
            case StatusType.Burn:
                vfxType = VFXType.Burn_Vignette;
                return true;

            case StatusType.Poison:
                vfxType = VFXType.Poison_Vignette;
                return true;

            //case StatusType.MoveSlow:
            //case StatusType.AttackSlow:
            //    vfxType = VFXType.Slow_Vignette;
            //    return true;

            //case StatusType.Stun:
            //    vfxType = VFXType.Stun_Vignette;
            //    return true;

            case StatusType.Root:
            case StatusType.Silence:
                vfxType = default;
                return false;

            default:
                vfxType = default;
                return false;
        }
    }

    #endregion                      //----------------------------------------


    #region Camera Caching          //----------------------------------------

    // Finds and caches the main camera transform.
    // Called once in Awake when _EnableCameraVFX is true.
    // Uses FindGameObjectWithTag once — result is stored for the object's lifetime.
    private void CacheCamera()
    {
        GameObject camObject = GameObject.FindGameObjectWithTag("MainCamera");

        if (camObject != null)
        {
            _cameraTransform = camObject.transform;
        }
        else
        {
            Debug.LogWarning("[Mb_StatusVFXHandler] Could not find a GameObject tagged " +
                             "'MainCamera'. Camera VFX will not play until the camera " +
                             "is found. Confirm the camera GameObject has the MainCamera tag.");
        }
    }

    #endregion                      //----------------------------------------
}