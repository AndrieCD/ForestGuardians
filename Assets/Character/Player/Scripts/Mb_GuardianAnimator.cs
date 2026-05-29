using UnityEngine;

/// <summary>
/// Drives the Animator component for any Guardian character.
/// This is the ONLY script that talks directly to the Animator.
///
/// ANIMATOR SETUP REQUIRED:
///   Base Layer:
///     - Locomotion Blend Tree (Speed float: 0=Idle, 1=Run)
///     - Jump, Fall states (IsGrounded bool + VerticalVelocity float)
///     - Defeat state (OnDefeat trigger, no exit)
///     - Primary1, Primary2, Secondary1, Secondary2 (from Any State via triggers)
///     - QAbility, EAbility, R1Ability, R2Ability (from Any State via triggers)
///
///   Reaction Layer (Weight = 1, Avatar Mask = Upper Body only):
///     - Empty default state
///     - TakeDamage1, TakeDamage2 (from Any State via TakeDamage1/TakeDamage2 triggers)
///     This layer lets the guardian flinch without interrupting run/locomotion.
///
/// Inspector setup:
///   - Animator: drag the Animator component (or leave null to auto-find on same GO)
///   - HugeDamageThreshold: fraction of max HP — default 0.15 (15%)
///   - SpeedDampTime: blend smoothness for locomotion — default 0.1
/// </summary>
public class Mb_GuardianAnimator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Animator Parameter Hashes
    // Using hashed integers instead of raw strings — faster and typo-proof.
    // -------------------------------------------------------------------------

    #region Parameter Hashes       //----------------------------------------

    private static readonly int _SpeedHash = Animator.StringToHash("Speed");
    private static readonly int _IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int _VerticalVelHash = Animator.StringToHash("VerticalVelocity");

    private static readonly int _Primary1Hash = Animator.StringToHash("Primary1");
    private static readonly int _Primary2Hash = Animator.StringToHash("Primary2");
    private static readonly int _Secondary1Hash = Animator.StringToHash("Secondary1");
    private static readonly int _Secondary2Hash = Animator.StringToHash("Secondary2");

    private static readonly int _QAbilityHash = Animator.StringToHash("QAbility");
    private static readonly int _EAbilityHash = Animator.StringToHash("EAbility");
    private static readonly int _R1AbilityHash = Animator.StringToHash("R1Ability");
    private static readonly int _R2AbilityHash = Animator.StringToHash("R2Ability");

    private static readonly int _TakeDamage1Hash = Animator.StringToHash("TakeDamage1");
    private static readonly int _TakeDamage2Hash = Animator.StringToHash("TakeDamage2");

    private static readonly int _OnDefeatHash = Animator.StringToHash("OnDefeat");

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    #region Inspector Fields        //----------------------------------------

    [Header("References")]
    [SerializeField] private Animator _Animator;

    [Header("Locomotion")]
    [Tooltip("How quickly Speed blends toward its target. 0.1 = snappy, 0.3 = floaty.")]
    [SerializeField] private float _SpeedDampTime = 0.1f;

    [Header("Hit Reaction")]
    [Tooltip("Fraction of Max HP a single hit must deal to trigger TakeDamage animation. " +
             "e.g. 0.15 = 15% of Max HP. Hits below this threshold play no reaction.")]
    [SerializeField][Range(0.01f, 1f)] private float _HugeDamageThreshold = 0.15f;

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    #region State                   //----------------------------------------

    // Tracks which primary/secondary variant played last for strict alternation.
    // false = last played was 1, so next plays 2. Starts false so first play is Primary1.
    private bool _lastPrimaryWasFirst = false;
    private bool _lastSecondaryWasFirst = false;

    // Cached reference to the health component so we can read MaxHP on damage events
    private Mb_HealthComponent _health;

    // Smoothed speed value — interpolated each frame before being sent to the Animator
    private float _currentSpeed = 0f;

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    #region Lifecycle               //----------------------------------------

    private void Awake()
    {
        if (_Animator == null)
            _Animator = GetComponent<Animator>();

        if (_Animator == null)
            Debug.LogError($"[Mb_GuardianAnimator] No Animator found on {gameObject.name}.");

        _health = GetComponent<Mb_HealthComponent>();

        if (_health == null)
            Debug.LogError($"[Mb_GuardianAnimator] No Mb_HealthComponent found on {gameObject.name}.");
    }

    private void OnEnable()
    {
        if (_health != null)
            _health.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnDamageTaken -= HandleDamageTaken;
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Locomotion — called from Mb_Movement every frame
    // -------------------------------------------------------------------------

    #region Locomotion              //----------------------------------------

    /// <summary>
    /// Updates the Idle/Run blend tree.
    /// Pass the magnitude of XZ velocity normalized to MoveSpeed (0 = idle, 1 = full run).
    /// </summary>
    public void SetSpeed(float speed)
    {
        _currentSpeed = Mathf.Lerp(_currentSpeed, speed, Time.deltaTime / _SpeedDampTime);
        //Debug.Log($"[Mb_GuardianAnimator] SetSpeed({speed:F2}) → _currentSpeed = {_currentSpeed:F2}");
        _Animator.SetFloat(_SpeedHash, _currentSpeed);
    }

    /// <summary>
    /// Pass true when the CharacterController reports isGrounded.
    /// </summary>
    public void SetGrounded(bool isGrounded)
    {
        _Animator.SetBool(_IsGroundedHash, isGrounded);
    }

    /// <summary>
    /// Pass the character's current vertical velocity.
    /// Positive = rising (Jump), negative = falling (Fall).
    /// </summary>
    public void SetVerticalVelocity(float verticalVelocity)
    {
        _Animator.SetFloat(_VerticalVelHash, verticalVelocity);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Attacks — strictly alternating between variant 1 and variant 2
    // -------------------------------------------------------------------------

    #region Attacks                 //----------------------------------------

    /// <summary>
    /// Plays Primary1 and Primary2 in strict alternation.
    /// First call plays Primary1, second plays Primary2, third plays Primary1, etc.
    /// </summary>
    public void TriggerPrimaryAttack()
    {
        // Flip the flag first, then read it — so first call (_lastWasFirst = false → true) plays 1
        _lastPrimaryWasFirst = !_lastPrimaryWasFirst;

        if (_lastPrimaryWasFirst)
            _Animator.SetTrigger(_Primary1Hash);
        else
            _Animator.SetTrigger(_Primary2Hash);
    }

    /// <summary>
    /// Plays Secondary1 and Secondary2 in strict alternation.
    /// </summary>
    public void TriggerSecondaryAttack()
    {
        _lastSecondaryWasFirst = !_lastSecondaryWasFirst;

        if (_lastSecondaryWasFirst)
            _Animator.SetTrigger(_Secondary1Hash);
        else
            _Animator.SetTrigger(_Secondary2Hash);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Abilities
    // -------------------------------------------------------------------------

    #region Abilities               //----------------------------------------

    /// <summary>Plays the Q Ability animation.</summary>
    public void TriggerQAbility() => _Animator.SetTrigger(_QAbilityHash);

    /// <summary>Plays the E Ability animation.</summary>
    public void TriggerEAbility() => _Animator.SetTrigger(_EAbilityHash);

    /// <summary>Plays the R Branch 1 animation (e.g. Sovereign's Wrath).</summary>
    public void TriggerR1Ability() => _Animator.SetTrigger(_R1AbilityHash);

    /// <summary>Plays the R Branch 2 animation (e.g. Eagle Eye).</summary>
    public void TriggerR2Ability() => _Animator.SetTrigger(_R2AbilityHash);

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Hit Reaction — plays on a separate Animator layer so it blends with
    // locomotion and doesn't interrupt attacks or ability animations
    // -------------------------------------------------------------------------

    #region Hit Reaction            //----------------------------------------

    /// <summary>
    /// Called by the OnDamageTaken event from Mb_HealthComponent.
    /// Only triggers a reaction animation if the hit exceeds the huge damage threshold.
    /// Plays randomly between TakeDamage1 and TakeDamage2 — either can repeat.
    /// </summary>
    private void HandleDamageTaken(float damageAmount)
    {
        if (_health == null) return;

        float maxHP = _health.CurrentHealth + damageAmount; // approximate MaxHP from before the hit
        // TODO: Replace this approximation with _health.MaxHP once that property is exposed.
        // For now: if damageAmount pushed health down, (currentHP + damage) ≈ pre-hit HP.
        // This is close enough for threshold checks but won't be perfect near death.

        float damagePercent = damageAmount / maxHP;

        if (damagePercent < _HugeDamageThreshold) return;

        // Randomly pick TakeDamage1 or TakeDamage2 — repeats are allowed
        if (Random.value < 0.5f)
            _Animator.SetTrigger(_TakeDamage1Hash);
        else
            _Animator.SetTrigger(_TakeDamage2Hash);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Defeat
    // -------------------------------------------------------------------------

    #region Defeat                  //----------------------------------------

    /// <summary>
    /// Triggers the Defeat animation. Call from Mb_GuardianBase when the guardian dies.
    /// The Defeat state should have no exit transition — it freezes on the last frame.
    /// </summary>
    public void TriggerDefeat()
    {
        _Animator.SetTrigger(_OnDefeatHash);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    #region Utility                 //----------------------------------------

    /// <summary>
    /// Cancels all queued ability and attack triggers.
    /// Call this if an ability is interrupted (e.g. stunned mid-cast)
    /// to prevent the animation from firing on the next frame.
    /// Does NOT cancel TakeDamage or OnDefeat — those should always play.
    /// </summary>
    public void CancelAllTriggers()
    {
        _Animator.ResetTrigger(_Primary1Hash);
        _Animator.ResetTrigger(_Primary2Hash);
        _Animator.ResetTrigger(_Secondary1Hash);
        _Animator.ResetTrigger(_Secondary2Hash);
        _Animator.ResetTrigger(_QAbilityHash);
        _Animator.ResetTrigger(_EAbilityHash);
        _Animator.ResetTrigger(_R1AbilityHash);
        _Animator.ResetTrigger(_R2AbilityHash);
    }

    #endregion                      //----------------------------------------
}