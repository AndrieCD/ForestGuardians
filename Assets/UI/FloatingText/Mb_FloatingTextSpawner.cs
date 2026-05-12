// Mb_FloatingTextSpawner.cs
// Sits on each CuBot GameObject as a component.
// Listens to that CuBot's combat events and requests floating text
// from Mb_FloatingTextPool when those events fire.
//
// POOL SAFETY:
//   CuBots are reactivated from a pool rather than re-instantiated, so
//   this component uses OnEnable/OnDisable for event subscriptions instead
//   of Awake/OnDestroy. This guarantees subscriptions are always fresh on
//   reuse and never stack up from previous activations.
//
// CRIT DETECTION:
//   OnDamageTaken only passes the damage amount — it doesn't know if the
//   hit was a crit. We detect crits by subscribing to the static
//   Sc_BaseAbility.OnCriticalHit event and checking if the damage amounts
//   match within a small tolerance window.
//
// Inspector Setup:
//   - Add this component to every CuBot prefab.
//   - Tune the font size, color, and spawn height fields per-prefab if needed.
//   - Mb_FloatingTextPool must exist in the scene (on the Stage Manager GameObject).

using UnityEngine;

public class Mb_FloatingTextSpawner : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields — Damage Text
    // -------------------------------------------------------------------------

    [Header("Damage Text — Size Scaling")]
    // TODO: Tune these values once you have a feel for typical damage ranges.
    // Suggested starting point: min=2, max=6, cap=500
    [SerializeField] private float _MinFontSize = 0.3f;
    [SerializeField] private float _MaxFontSize = 0.9f;
    [SerializeField] private float _DamageSizeCap = 1500f;    // Damage at which font hits maximum size

    [Header("Damage Text — Colors")]
    [SerializeField] private Color _NormalHitColor = Color.white;
    [SerializeField] private Color _CritHitColor = new Color(1f, 0.85f, 0f); // Gold

    [Header("Heal Text")]
    [SerializeField] private float _HealFontSize = 3f;
    [SerializeField] private Color _HealColor = new Color(0.4f, 1f, 0.4f);  // Soft green

    [Header("Status Text")]
    // TODO: Expand this into a Dictionary<StatusType, Color> once status effects are formalized.
    // For now, callers pass the color directly via ShowStatus().
    [SerializeField] private float _StatusFontSize = 2.5f;

    [Header("Spawn Position")]
    // How far above the GameObject's pivot the text appears.
    // Adjust per-prefab so text clears the CuBot's mesh.
    // TODO: Tune per CuBot type — a tall CuBot like Minny needs a higher offset than Chopper.
    [SerializeField] private float _SpawnHeightOffset = 2f;


    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    // Cached component references — fetched once in Awake, never via GetComponent at runtime
    private Mb_HealthComponent _health;

    // Tracks whether the most recent damage event was a crit.
    // Set by OnCriticalHit (which fires first), read by HandleDamageTaken (which fires after).
    // This works because OnCriticalHit is invoked inside ApplyCriticalStrike(),
    // which runs before TakeDamage() is called — so the flag is always set before we need it.
    private bool _pendingCrit = false;

    // The crit damage value from the last OnCriticalHit — used to match against
    // the incoming OnDamageTaken amount to confirm the crit was for this CuBot.
    // Without this check, a crit on a different CuBot would incorrectly flag this one.
    private float _pendingCritDamage = 0f;

    // Tolerance for float comparison between crit damage and damage taken.
    // Damage values should match exactly, but floating point math warrants a small buffer.
    private const float CRIT_MATCH_TOLERANCE = 0.01f;


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Cache references once — GetComponent is slow and we can't call it in event handlers
        _health = GetComponent<Mb_HealthComponent>();

        if (_health == null)
            Debug.LogError($"[Mb_FloatingTextSpawner] No Mb_HealthComponent found on {gameObject.name}.");
    }


    private void OnEnable()
    {
        // Subscribe every time the CuBot activates from the pool —
        // this guarantees no stale or missing subscriptions across reuses
        if (_health != null)
        {
            _health.OnDamageTaken += HandleDamageTaken;
            _health.OnHealReceived += HandleHealReceived;
        }

        // Static event — one subscription catches crits from every ability slot.
        // We filter by damage amount match in the handler so we only react to
        // crits that actually hit this specific CuBot.
        Sc_BaseAbility.OnCriticalHit += HandleCriticalHit;
    }


    private void OnDisable()
    {
        // Unsubscribe on deactivation — prevents ghost listeners while pooled
        if (_health != null)
        {
            _health.OnDamageTaken -= HandleDamageTaken;
            _health.OnHealReceived -= HandleHealReceived;
        }

        Sc_BaseAbility.OnCriticalHit -= HandleCriticalHit;

        // Clear crit state so a pooled reuse doesn't inherit a stale pending crit
        _pendingCrit = false;
        _pendingCritDamage = 0f;
    }


    // -------------------------------------------------------------------------
    // Event Handlers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fired by Sc_BaseAbility.OnCriticalHit before TakeDamage is called.
    /// We store the crit damage so HandleDamageTaken can recognize it.
    /// </summary>
    private void HandleCriticalHit(float critDamage, Mb_CharacterBase attacker)
    {
        // We can't confirm which CuBot was hit here — OnCriticalHit is static
        // and fires before the damage reaches any specific target. So we store
        // the amount and let HandleDamageTaken confirm the match.
        _pendingCrit = true;
        _pendingCritDamage = critDamage;
    }


    /// <summary>
    /// Fired by Mb_HealthComponent.OnDamageTaken with the raw damage amount.
    /// Checks for a pending crit match, then requests a damage number from the pool.
    /// </summary>
    private void HandleDamageTaken(float amount)
    {
        if (Mb_FloatingTextPool.Instance == null) return;

        // Check if this damage matches the pending crit amount
        bool isCrit = _pendingCrit &&
                      Mathf.Abs(amount - _pendingCritDamage) <= CRIT_MATCH_TOLERANCE;

        // Consume the pending crit regardless of match outcome —
        // if it didn't match, the crit was for a different CuBot and we discard it
        _pendingCrit = false;
        _pendingCritDamage = 0f;

        Vector3 spawnPos = transform.position + Vector3.up * _SpawnHeightOffset;

        Mb_FloatingTextPool.Instance.SpawnDamageText(
            spawnPos,
            amount,
            isCrit,
            _NormalHitColor,
            _CritHitColor,
            _MinFontSize,
            _MaxFontSize,
            _DamageSizeCap
        );
    }


    /// <summary>
    /// Fired by Mb_HealthComponent.OnHealReceived with the heal amount.
    /// System is wired and ready — spawn behavior can be toggled here when needed.
    /// </summary>
    private void HandleHealReceived(float amount)
    {
        // TODO: Decide whether CuBot heals should display to the player.
        // If yes, uncomment the block below. If heals should only show on the Guardian,
        // move Mb_FloatingTextSpawner (or a Guardian-specific variant) onto the player prefab.

        if (Mb_FloatingTextPool.Instance == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * _SpawnHeightOffset;

        Mb_FloatingTextPool.Instance.SpawnDamageText(
            spawnPos,
            amount,
            false,
            _HealColor,
            _HealColor,
            _HealFontSize,
            _HealFontSize,
            1f           // Size cap irrelevant — font is fixed for heals
        );
    }


    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called directly by ability or augment scripts to show a status label
    /// above this CuBot. Example: spawner.ShowStatus("SLOW", Color.cyan);
    ///
    /// This is intentionally separate from the health events because
    /// OnStatsChanged does not carry enough information to determine what
    /// status was applied or what color to use — the caller knows both.
    /// </summary>
    /// <param name="label">Short status label, e.g. "SLOW", "STUN", "POISONED".</param>
    /// <param name="color">Color associated with this status type.</param>
    /// <param name="icon">Optional icon sprite to display alongside the label.</param>
    public void ShowStatus(string label, Color color, Sprite icon = null)
    {
        if (Mb_FloatingTextPool.Instance == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * _SpawnHeightOffset;

        Mb_FloatingTextPool.Instance.SpawnStatusText(
            spawnPos,
            label,
            color,
            _StatusFontSize,
            icon
        );
    }
}