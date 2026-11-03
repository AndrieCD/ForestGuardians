using System.Collections;
using UnityEngine;

// =============================================================================
// Mb_MindspikesZone.cs
// Attached to one Mindspikes tile prefab. Handles trigger detection, damage
// ticking, slow application, visual scaling, and self-destruction after duration
// expires.
//
// Kept as a separate class (not an inner class) so Unity can serialize it
// as a prefab component and so Leo can reference it in the Editor.
// =============================================================================


public class Mb_MindspikesZone : MonoBehaviour
{
    private const float EXIT_TRANSITION_DURATION = 0.2f;

    // -------------------------------------------------------------------------
    // Runtime State (set by Initialize())
    // -------------------------------------------------------------------------

    private Mb_CharacterBase _owner;
    private float _damage;
    private float _tickInterval;
    private float _slowPercent;
    private float _slowDuration;
    private float _duration;
    private bool _isOvercharged;
    private bool _isDeactivated;

    // Shared by every tile from the same cast so overlapping tiles cannot
    // multiply the damage tick rate.
    private Sc_MindspikesTickTracker _tickTracker = new Sc_MindspikesTickTracker();

    // The BoxCollider on this GO — resized to match final dimensions in Initialize()
    private BoxCollider _collider;

    // Looping field VFX child — located by name, plays while zone is active
    private ParticleSystem _fieldVFX;

    // The visual mesh root — scaled to match collider dimensions
    // TODO: Leo — name the spike mesh child GO "SpikeVisualRoot" on the prefab
    private Transform _visualRoot;


    // -------------------------------------------------------------------------
    // Initialize
    // Called by Mari_Q immediately after Instantiate()
    // -------------------------------------------------------------------------

    public void Initialize(
        Mb_CharacterBase owner,
        float damage,
        float tickInterval,
        float slowPercent,
        float slowDuration,
        float length,
        float width,
        float height,
        float duration,
        bool isOvercharged,
        Sc_MindspikesTickTracker sharedTickTracker = null)
    {
        _owner = owner;
        _damage = damage;
        _tickInterval = tickInterval;
        _slowPercent = slowPercent;
        _slowDuration = slowDuration;
        _duration = duration;
        _isOvercharged = isOvercharged;
        _isDeactivated = false;
        _tickTracker = sharedTickTracker ?? new Sc_MindspikesTickTracker();

        // --- Resize BoxCollider to match final resolved dimensions ---
        _collider = GetComponent<BoxCollider>();
        if (_collider != null)
        {
            _collider.isTrigger = true;
            // BoxCollider.size is in local space — width=X, height=Y, length=Z.
            // The tile root's local up follows the ground normal assigned by
            // Mari_Q, so this height extends away from the local ground surface.
            _collider.size = new Vector3(width, height, length);
            _collider.center = new Vector3(0f, height / 2f, 0f);
        }
        else
        {
            Debug.LogError("[Mb_MindspikesZone] No BoxCollider found on spike zone prefab.");
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // --- Scale visual root to match collider ---
        // SpikeVisualRoot is a child GO whose localScale drives the mesh size.
        // We scale it to (width, 1, length) so the visual stays flat on the
        // tile's projected ground plane regardless of trigger height.
        _visualRoot = transform.Find("SpikeVisualRoot");
        if (_visualRoot != null)
            _visualRoot.localScale = new Vector3(width, 1f, length);

        // --- Locate and play field VFX ---
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "MindspikesFieldVFX")
            {
                _fieldVFX = ps;
                break;
            }
        }
        _fieldVFX?.Play();

        // --- Overcharged visual tint ---
        // TODO: If the spike zone has a Renderer, tint it a different color
        // when overcharged so the player can visually distinguish the empowered field.
        // Suggested: overcharged = brighter purple/white pulse vs. normal blue-green.
        // Implement once Leo finalizes the material setup on the prefab.
        if (isOvercharged)
        {
            // Placeholder: scale the VFX emission rate up for overcharge intensity
            if (_fieldVFX != null)
            {
                var emission = _fieldVFX.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                    emission.rateOverTime.constant * 2f
                );
            }
        }

        // Start the self-destruct timer
        StartCoroutine(DurationRoutine());
    }


    // -------------------------------------------------------------------------
    // Trigger Detection
    // -------------------------------------------------------------------------

    private void OnTriggerStay(Collider other)
    {
        if (_isDeactivated) return;
        if (_owner != null && other.gameObject == _owner.gameObject) return;

        MB_CuBotBase enemy = other.GetComponentInParent<MB_CuBotBase>();
        if (enemy == null) return;
        if (enemy.Health == null || enemy.Health.IsDead) return;

        if (!_tickTracker.TryConsumeTick(enemy, _tickInterval)) return;

        ApplyDamageAndSlow(enemy);
    }

    private void OnTriggerExit(Collider other)
    {
        // Do not clear the shared tick tracker here. A CuBot can leave one tile
        // while still touching another, and clearing on per-tile exit lets
        // neighboring tiles stack damage before the cast-level interval expires.
    }


    // -------------------------------------------------------------------------
    // Damage and Slow Application
    // -------------------------------------------------------------------------

    private void ApplyDamageAndSlow(MB_CuBotBase enemy)
    {
        // --- Damage ---
        enemy.Health.TakeDamage(_damage);

        Mb_StatusEffectController statusController =
            enemy.GetComponent<Mb_StatusEffectController>();

        statusController?.Apply(Sc_StatusEffect.MoveSlow(_slowDuration, _slowPercent));

        Debug.Log($"[Mb_MindspikesZone] Hit {enemy.CharacterName} for {_damage} " +
                  $"and applied {_slowPercent}% slow.");
    }


    // -------------------------------------------------------------------------
    // Duration and Cleanup
    // -------------------------------------------------------------------------

    private IEnumerator DurationRoutine()
    {
        yield return new WaitForSeconds(_duration);
        DeactivateZone();
    }


    private void DeactivateZone()
    {
        if (_isDeactivated) return;

        _isDeactivated = true;

        // Stop emitting before the exit transition so existing particles can
        // finish naturally while the complete VFX root scales down.
        if (_fieldVFX != null)
            _fieldVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Disable the collider immediately so no more damage ticks fire
        // during the VFX exit transition.
        if (_collider != null)
            _collider.enabled = false;

        StartCoroutine(Sc_VFXExitTransition.ScaleDownAndDestroy(
            transform,
            EXIT_TRANSITION_DURATION
        ));
    }
}
