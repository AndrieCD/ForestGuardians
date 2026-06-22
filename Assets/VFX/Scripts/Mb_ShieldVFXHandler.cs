// Mb_ShieldVFXHandler.cs
// Generic shield VFX handler — works on ANY character with Mb_HealthComponent
// (Guardian or CuBot). Spawns a looping shield VFX whenever the character has
// an active shield, and stops it the instant the shield reaches zero —
// whether that's from damage absorption or natural expiry.
//
// WHY THIS LIVES SEPARATELY FROM THE ABILITY:
//   Shields can come from any source (Sky Rend, an augment, a future CuBot
//   shield mechanic, etc). The ability that grants the shield shouldn't need
//   to know anything about VFX — it just calls Mb_HealthComponent.AddShield().
//   This handler reacts to the character's shield STATE, not to who caused it.
//
// HOW IT DETECTS SHIELD START/END:
//   - OnShieldAdded fires the moment any shield is granted — used to start the VFX.
//   - OnShieldChanged fires whenever shield changes for ANY reason (damage
//     absorption, natural expiry, additional shields stacking). We check
//     CurrentShield here — once it hits 0, we stop the VFX immediately,
//     regardless of whether it expired naturally or was fully depleted by damage.
//
// POOL SAFETY:
//   Uses OnEnable/OnDisable (not Awake/OnDestroy) so CuBots that gain a shield,
//   die, and get pool-reused don't carry a stale VFX subscription or orphaned VFX.
//
// Inspector setup:
//   - Attach this to ANY character prefab (Guardian or CuBot) that can receive shields.
//   - Requires Mb_HealthComponent on the same GameObject.
//   - No fields to assign — everything is automatic.

using UnityEngine;

public class Mb_ShieldVFXHandler : MonoBehaviour
{
    private Mb_HealthComponent _health;

    // Tracks whether the VFX is currently playing, so we don't call Play()
    // redundantly every time OnShieldChanged fires from a partial absorb
    private bool _isShieldVFXActive = false;


    private void Awake()
    {
        _health = GetComponent<Mb_HealthComponent>();

        if (_health == null)
            Debug.LogError($"[Mb_ShieldVFXHandler] No Mb_HealthComponent found on {gameObject.name}.");
    }


    private void OnEnable()
    {
        if (_health == null) return;

        _health.OnShieldAdded -= HandleShieldAdded;
        _health.OnShieldAdded += HandleShieldAdded;

        _health.OnShieldChanged -= HandleShieldChanged;
        _health.OnShieldChanged += HandleShieldChanged;
    }


    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnShieldAdded -= HandleShieldAdded;
            _health.OnShieldChanged -= HandleShieldChanged;
        }

        // Force-stop the VFX if this character is disabled while shielded
        // (e.g. CuBot dies and returns to pool, or Guardian scene teardown).
        // Prevents an orphaned shield VFX from lingering after the character is gone.
        if (_isShieldVFXActive)
        {
            Mb_VFXManager.Stop(VFXType.Status_Shield, gameObject);
            _isShieldVFXActive = false;
        }
    }


    // Fired the moment ANY shield is granted (amount > 0).
    // Starts the VFX if it isn't already running — multiple shield sources
    // stacking won't spawn duplicate VFX instances since Mb_VFXManager already
    // handles "replace existing instance on this target" internally.
    private void HandleShieldAdded(float amountAdded)
    {
        if (_isShieldVFXActive) return; // Already playing — nothing to do

        Mb_VFXManager.Play(VFXType.Status_Shield, transform.position, transform);
        _isShieldVFXActive = true;
    }


    // Fired whenever shield changes for any reason — damage absorption,
    // natural expiry, or a new shield stacking on top.
    // We only care about the moment it reaches zero — that's when the VFX should stop.
    private void HandleShieldChanged(float currentShield)
    {
        if (currentShield <= 0f && _isShieldVFXActive)
        {
            Mb_VFXManager.Stop(VFXType.Status_Shield, gameObject);
            _isShieldVFXActive = false;
        }
    }
}