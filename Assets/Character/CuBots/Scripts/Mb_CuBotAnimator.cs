using UnityEngine;

/// <summary>
/// Drives the Animator component for basic CuBot enemies.
/// This is the ONLY script that talks directly to a CuBot's Animator.
/// Mirror of Mb_GuardianAnimator — same architecture, stripped to CuBot needs.
///
/// ANIMATOR CONTROLLER SETUP REQUIRED:
///   Base Layer:
///     - Idle/Run Blend Tree  (Speed float: 0 = Idle, >0 = Run)
///         Entry → Idle/Run Blend Tree (default state)
///     - Attack state         (from Any State via Attack trigger; has exit back to Blend Tree)
///     - Aggro state          (from Any State via Aggro trigger; one-shot, exits to Blend Tree)
///     - Death state          (from Any State via OnDeath trigger; no exit — freeze on last frame)
///
///   Reaction Layer (Weight = 1, Avatar Mask = Full Body or Upper Body):
///     - Empty default state
///     - Hit state            (from Any State via Hit trigger; short flinch, auto-exits)
///     Using a separate layer means a hit flinch can play without interrupting
///     the Run or Aggro animations on the Base Layer.
///
///   Animator Parameters to create (exact names — must match the hashes below):
///     Speed    — Float   — drives Idle/Run blend tree
///     Attack   — Trigger — fires the attack animation
///     Aggro    — Trigger — fires the aggro reaction (one-shot)
///     Hit      — Trigger — fires the hit flinch on the Reaction Layer
///     OnDeath  — Trigger — fires the death animation (never cancelled)
///
/// Inspector setup:
///   - Animator:             drag the Animator component, or leave null to auto-find on same GO
///   - SpeedDampTime:        blend smoothness — 0.1 is snappy, 0.3 is floaty (default 0.1)
///   - HitDamageThreshold:   fraction of max HP a single hit must exceed to play flinch
///                           e.g. 0.10 = 10% of max HP (default 0.10)
///
/// How to wire this up on a CuBot prefab:
///   1. Add Mb_CuBotAnimator as a sibling component alongside Mb_ChopperController (or any controller).
///   2. Assign the Animator in the Inspector (or leave null — Awake will find it).
///   3. In the controller's UpdateAnimator(), call: _cuBotAnimator.SetSpeed(_Agent.velocity.magnitude);
///   4. In the controller's OnTargetChanged(), call: _cuBotAnimator.TriggerAggro() when the new
///      target is the Player.
///   5. In the ability script (e.g. Sc_ChopperMeleeAttack), call: _cuBotAnimator.TriggerAttack()
///      at the moment the attack animation should play.
/// </summary>
public class Mb_CuBotAnimator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Animator Parameter Hashes
    // StringToHash converts the string once at class load time.
    // Using the int hash at runtime is much faster than passing raw strings every frame.
    // -------------------------------------------------------------------------

    #region Parameter Hashes       //----------------------------------------

    private static readonly int _SpeedHash = Animator.StringToHash("Speed");
    private static readonly int _AttackHash = Animator.StringToHash("Attack");
    private static readonly int _AggroHash = Animator.StringToHash("Aggro");
    private static readonly int _HitHash = Animator.StringToHash("Hit");
    private static readonly int _OnDeathHash = Animator.StringToHash("OnDeath");

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    #region Inspector Fields        //----------------------------------------

    [Header("References")]
    [Tooltip("The Animator component on this CuBot. Leave null to auto-find on the same GameObject.")]
    [SerializeField] private Animator _Animator;

    [Header("Locomotion")]
    [Tooltip("How quickly Speed blends toward its target value each frame. " +
             "0.1 = snappy (good for fast enemies like Chopper), 0.3 = floaty.")]
    // TODO: Tune per CuBot type once animations are in — Chopper feels better at 0.08, Hunter at 0.15
    [SerializeField] private float _SpeedDampTime = 0.1f;

    [Header("Hit Reaction")]
    [Tooltip("Fraction of Max HP a single hit must deal to trigger the Hit flinch animation. " +
             "e.g. 0.10 = 10% of max HP. Small chip-damage hits play no reaction. " +
             "Raise this if CuBots flinch too often; lower it if they seem too stoic.")]
    // TODO: Default 0.10 — CuBots are tougher-feeling than Guardians, so threshold is lower than Guardian's 0.15
    [SerializeField][Range(0.01f, 1f)] private float _HitDamageThreshold = 0.10f;

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    #region State                   //----------------------------------------

    // Cached reference to this CuBot's health component — fetched once in Awake.
    // Used in HandleDamageTaken to calculate what fraction of max HP was dealt.
    private Mb_HealthComponent _health;

    // The smoothed speed value we send to the Animator each frame.
    // We lerp toward the target speed rather than snapping so the blend tree
    // transitions look smooth even when the CuBot stops abruptly.
    private float _currentSpeed = 0f;

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    #region Lifecycle               //----------------------------------------

    private void Awake()
    {
        // Auto-find Animator if not assigned in Inspector
        if (_Animator == null)
            _Animator = GetComponent<Animator>();

        if (_Animator == null)
            Debug.LogError($"[Mb_CuBotAnimator] No Animator found on {gameObject.name}. " +
                           "Add an Animator component or assign it in the Inspector.");

        // Cache the health component — we need it to calculate hit damage percentage
        _health = GetComponent<Mb_HealthComponent>();

        if (_health == null)
            Debug.LogError($"[Mb_CuBotAnimator] No Mb_HealthComponent found on {gameObject.name}. " +
                           "Hit reactions will not play.");
    }


    private void OnEnable()
    {
        // CuBots are pooled — OnEnable fires every time one is reactivated from the pool,
        // not just on first spawn. We subscribe here (not in Awake) so the subscription
        // is always fresh and never accumulates duplicate listeners across reuses.

        if (_health != null)
        {
            // Unsubscribe first as a safety net — guards against double-subscribe
            // if OnEnable fires before a previous OnDisable (shouldn't happen, but defensive)
            _health.OnDamageTaken -= HandleDamageTaken;
            _health.OnDamageTaken += HandleDamageTaken;

            _health.OnDeath -= HandleDeath;
            _health.OnDeath += HandleDeath;
        }

        // Reset smoothed speed so a pool-reused CuBot doesn't inherit the last
        // frame's speed from its previous activation and start blending from there
        _currentSpeed = 0f;

        if (_Animator != null)
            _Animator.SetFloat(_SpeedHash, 0f);
    }


    private void OnDisable()
    {
        // Unsubscribe on deactivation (death, pool return).
        // Without this, the event would hold a reference to a deactivated CuBot
        // and could fire HandleDamageTaken or HandleDeath on a "dead" instance.
        if (_health != null)
        {
            _health.OnDamageTaken -= HandleDamageTaken;
            _health.OnDeath -= HandleDeath;
        }
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Locomotion — called from Mb_CuBotController.UpdateAnimator() every frame
    // -------------------------------------------------------------------------

    #region Locomotion              //----------------------------------------

    /// <summary>
    /// Smoothly drives the Idle/Run blend tree.
    /// Call this from the controller's UpdateAnimator() every frame, passing
    /// _Agent.velocity.magnitude as the speed value.
    /// The value is lerped so blend tree transitions look smooth.
    /// </summary>
    /// <param name="speed">Current movement speed — typically NavMeshAgent.velocity.magnitude.</param>
    public void SetSpeed(float speed)
    {
        // Lerp toward the target speed — avoids a jarring snap between Idle and Run
        _currentSpeed = Mathf.Lerp(_currentSpeed, speed, Time.deltaTime / _SpeedDampTime);
        _Animator.SetFloat(_SpeedHash, _currentSpeed);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Attack — called from ability scripts (e.g. Sc_ChopperMeleeAttack)
    // -------------------------------------------------------------------------

    #region Attack                  //----------------------------------------

    /// <summary>
    /// Fires the Attack trigger to play the attack animation.
    /// Call this from the ability script at the moment the animation should start
    /// (typically at the beginning of the windup, or at the moment of impact).
    /// Basic CuBots have one attack clip — no alternation needed.
    /// </summary>
    public void TriggerAttack()
    {
        if (_Animator == null) return;
        _Animator.SetTrigger(_AttackHash);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Aggro — called from Mb_CuBotController.OnTargetChanged()
    // -------------------------------------------------------------------------

    #region Aggro                   //----------------------------------------

    /// <summary>
    /// Fires the Aggro trigger to play the aggro reaction animation.
    /// Call this from OnTargetChanged() in the controller ONLY when the new
    /// target is the Player (not when switching back to Panoharra).
    /// The animation is one-shot and exits automatically back to the Blend Tree.
    /// </summary>
    public void TriggerAggro()
    {
        if (_Animator == null) return;
        _Animator.SetTrigger(_AggroHash);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Hit Reaction — driven by Mb_HealthComponent.OnDamageTaken event
    // -------------------------------------------------------------------------

    #region Hit Reaction            //----------------------------------------

    private void HandleDamageTaken(float damageAmount)
    {
        if (_Animator == null || _health == null) return;

        // Approximate max HP from before the hit landed:
        // currentHealth was already reduced before this event fires,
        // so (currentHealth + damageAmount) ≈ health just before the hit.
        // This matches the same approximation used in Mb_GuardianAnimator.
        // TODO: Replace with _health.GetMaxHealth() once that accessor is reliable post-damage.
        float approxMaxHP = _health.CurrentHealth + damageAmount;

        // Guard against divide-by-zero (should never happen, but be safe)
        if (approxMaxHP <= 0f) return;

        float damagePercent = damageAmount / approxMaxHP;

        // Only play the flinch if the hit was significant enough —
        // small chip-damage hits should not interrupt the run animation
        if (damagePercent < _HitDamageThreshold) return;

        _Animator.SetTrigger(_HitHash);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Death — driven by Mb_HealthComponent.OnDeath event
    // -------------------------------------------------------------------------

    #region Death                   //----------------------------------------

    private void HandleDeath()
    {
        if (_Animator == null) return;

        // Fire the death trigger — the Death state should have no exit transition
        // so the CuBot freezes on its last death frame until the GameObject is deactivated.
        _Animator.SetTrigger(_OnDeathHash);

        // Unsubscribe immediately after death fires.
        // The CuBot GameObject will be deactivated shortly after by MB_CuBotBase.HandleDeath(),
        // which will also trigger OnDisable and clean up remaining subscriptions.
        // Unsubscribing here is an extra safety net: if the pool reactivates this CuBot
        // before OnDisable fully cleans up, we won't double-fire death on a live enemy.
        if (_health != null)
        {
            _health.OnDeath -= HandleDeath;
            _health.OnDamageTaken -= HandleDamageTaken;
        }
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    #region Utility                 //----------------------------------------

    /// <summary>
    /// Cancels all queued combat triggers except OnDeath.
    /// Call this if a CuBot is interrupted mid-animation (e.g. stunned, displaced)
    /// to prevent a stale Attack or Aggro trigger from firing on the next frame.
    /// OnDeath is intentionally excluded — death should always play.
    /// </summary>
    public void CancelAllTriggers()
    {
        if (_Animator == null) return;

        _Animator.ResetTrigger(_AttackHash);
        _Animator.ResetTrigger(_AggroHash);
        _Animator.ResetTrigger(_HitHash);
        // OnDeath is deliberately NOT reset here — death must never be cancelled
    }

    #endregion                      //----------------------------------------
}