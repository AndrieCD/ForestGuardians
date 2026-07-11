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
///     - QAbility, EAbility, R1Ability, R2Ability
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

    private bool _lastPrimaryWasFirst = false;
    private bool _lastSecondaryWasFirst = false;

    private Mb_HealthComponent _health;

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

    public void SetSpeed(float speed)
    {
        _currentSpeed = Mathf.Lerp(_currentSpeed, speed, Time.deltaTime / _SpeedDampTime);
        _Animator.SetFloat(_SpeedHash, _currentSpeed);
    }

    public void SetGrounded(bool isGrounded)
    {
        _Animator.SetBool(_IsGroundedHash, isGrounded);
    }

    public void SetVerticalVelocity(float verticalVelocity)
    {
        _Animator.SetFloat(_VerticalVelHash, verticalVelocity);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Attacks — strictly alternating between variant 1 and variant 2
    // -------------------------------------------------------------------------

    #region Attacks                 //----------------------------------------

    public void TriggerPrimaryAttack()
    {
        _lastPrimaryWasFirst = !_lastPrimaryWasFirst;

        if (_lastPrimaryWasFirst)
            _Animator.SetTrigger(_Primary1Hash);
        else
            _Animator.SetTrigger(_Primary2Hash);
    }

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

    /// <summary>Enables the R Branch 1 animation state.</summary>
    public void TriggerR1Ability()
    {
        Debug.Log("Triggering R1 Ability animation.");
        _Animator.SetBool(_R1AbilityHash, true);
    }

    /// <summary>Disables the R Branch 1 animation state.</summary>
    public void EndR1Ability() => _Animator.SetBool(_R1AbilityHash, false);

    /// <summary>Enables the R Branch 2 animation state.</summary>
    public void TriggerR2Ability()
    {
        Debug.Log("Triggering R2 Ability animation.");
        _Animator.SetBool(_R2AbilityHash, true);
    }

    /// <summary>Disables the R Branch 2 animation state.</summary>
    public void EndR2Ability()
    {
        Debug.Log("Ending R2 Ability animation.");
        _Animator.SetBool(_R2AbilityHash, false);
    }
    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Hit Reaction
    // -------------------------------------------------------------------------

    #region Hit Reaction            //----------------------------------------

    private void HandleDamageTaken(float damageAmount)
    {
        if (_health == null) return;

        float maxHP = _health.CurrentHealth + damageAmount;

        float damagePercent = damageAmount / maxHP;

        if (damagePercent < _HugeDamageThreshold) return;

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

    public void TriggerDefeat()
    {
        _Animator.SetTrigger(_OnDefeatHash);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    #region Utility                 //----------------------------------------

    public void CancelAllTriggers()
    {
        _Animator.ResetTrigger(_Primary1Hash);
        _Animator.ResetTrigger(_Primary2Hash);
        _Animator.ResetTrigger(_Secondary1Hash);
        _Animator.ResetTrigger(_Secondary2Hash);
        _Animator.ResetTrigger(_QAbilityHash);
        _Animator.ResetTrigger(_EAbilityHash);

        _Animator.SetBool(_R1AbilityHash, false);
        _Animator.SetBool(_R2AbilityHash, false);
    }

    #endregion                      //----------------------------------------
}
