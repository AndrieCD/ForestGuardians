using UnityEngine;

/// <summary>
/// Drives the Animator component for any Guardian character.
/// This script is the ONLY thing that should talk directly to the Animator.
/// Guardian classes call the public methods here instead of setting
/// animator parameters themselves — keeps animation logic in one place.
/// 
/// Attach this to the same GameObject as your Guardian's Animator.
/// </summary>
public class Mb_GuardianAnimator : MonoBehaviour
{
    // --- Animator Parameter Name Constants ---
    // We use constants instead of typing raw strings everywhere.
    // If you rename a parameter in the Animator, you only fix it here.
    private static readonly int _SpeedHash = Animator.StringToHash("Speed");
    private static readonly int _IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int _VerticalVelHash = Animator.StringToHash("VerticalVelocity");
    private static readonly int _Attack1Hash = Animator.StringToHash("Attack1");
    private static readonly int _Attack2Hash = Animator.StringToHash("Attack2");
    private static readonly int _QAbilityHash = Animator.StringToHash("QAbility");
    private static readonly int _EAbilityHash = Animator.StringToHash("EAbility");
    private static readonly int _R1AbilityHash = Animator.StringToHash("R1Ability");
    private static readonly int _R2AbilityHash = Animator.StringToHash("R2Ability");
    private static readonly int _OnDefeatHash = Animator.StringToHash("OnDefeat");

    [Header("References")]
    [SerializeField] private Animator _Animator;

    // How quickly the Speed float blends toward the target value.
    // A value of 10 feels snappy; lower it (e.g. 5) for a heavier feel.
    [SerializeField] private float _SpeedDampTime = 0.1f;

    // --- Internal State ---
    private float _currentSpeed = 0f;

    private void Awake( )
    {
        // If the animator wasn't assigned in the Inspector, try to find it
        // on this same GameObject as a fallback.
        if (_Animator == null)
            _Animator = GetComponent<Animator>( );

        if (_Animator == null)
            Debug.LogError($"[Mb_GuardianAnimator] No Animator found on {gameObject.name}. " +
                           "Assign it in the Inspector.");
    }

    // ---------------------------------------------------------------
    // LOCOMOTION
    // Call these from Mb_Movement or Mb_GuardianBase every frame.
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates the movement speed blend for the Idle/Run blend tree.
    /// Pass in the magnitude of the character's XZ velocity (0 = idle, 1 = running).
    /// </summary>
    public void SetSpeed(float speed)
    {
        // Smoothly damp toward the target speed so transitions don't pop.
        _currentSpeed = Mathf.Lerp(_currentSpeed, speed, Time.deltaTime / _SpeedDampTime);
        _Animator.SetFloat(_SpeedHash, _currentSpeed);
    }

    /// <summary>
    /// Tell the animator whether the character is on the ground.
    /// Used to switch between Jump and Fall states.
    /// </summary>
    public void SetGrounded(bool isGrounded)
    {
        _Animator.SetBool(_IsGroundedHash, isGrounded);
    }

    /// <summary>
    /// Pass in the character's current vertical velocity (positive = rising, negative = falling).
    /// The Animator uses this to decide whether to play Jump or Fall.
    /// </summary>
    public void SetVerticalVelocity(float verticalVelocity)
    {
        _Animator.SetFloat(_VerticalVelHash, verticalVelocity);
    }

    // ---------------------------------------------------------------
    // ATTACKS & ABILITIES
    // These are fire-and-forget. Call once when the action starts.
    // The Animator handles returning to locomotion via exit time / transitions.
    // ---------------------------------------------------------------

    /// <summary>Plays the Primary Attack animation.</summary>
    public void TriggerPrimaryAttack( )
    {
        _Animator.SetTrigger(_Attack1Hash);
    }

    /// <summary>Plays the Secondary Attack animation.</summary>
    public void TriggerSecondaryAttack( )
    {
        _Animator.SetTrigger(_Attack2Hash);
    }

    /// <summary>Plays the Q Ability animation.</summary>
    public void TriggerQAbility( )
    {
        _Animator.SetTrigger(_QAbilityHash);
    }

    /// <summary>Plays the E Ability animation.</summary>
    public void TriggerEAbility( )
    {
        _Animator.SetTrigger(_EAbilityHash);
    }

    /// <summary>Plays the R1 Ability animation (e.g. Feather Barrage / Psychic Bloom).</summary>
    public void TriggerR1Ability( )
    {
        _Animator.SetTrigger(_R1AbilityHash);
    }

    /// <summary>Plays the R2 Ability animation (e.g. Eagle Eye / Mind Unbound).</summary>
    public void TriggerR2Ability( )
    {
        _Animator.SetTrigger(_R2AbilityHash);
    }

    // ---------------------------------------------------------------
    // DEFEAT
    // ---------------------------------------------------------------

    /// <summary>
    /// Triggers the Defeat animation. Call this from Mb_GuardianBase.Die().
    /// Once Defeat plays, the Animator should have no exit — it stays on
    /// the last frame until the game handles the end state.
    /// </summary>
    public void TriggerDefeat( )
    {
        _Animator.SetTrigger(_OnDefeatHash);
    }

    // ---------------------------------------------------------------
    // UTILITY
    // ---------------------------------------------------------------

    /// <summary>
    /// Cancels any queued triggers — useful when an ability is interrupted
    /// (e.g. guardian takes a knockback hit mid-cast).
    /// </summary>
    public void CancelAllTriggers( )
    {
        _Animator.ResetTrigger(_Attack1Hash);
        _Animator.ResetTrigger(_Attack2Hash);
        _Animator.ResetTrigger(_QAbilityHash);
        _Animator.ResetTrigger(_EAbilityHash);
        _Animator.ResetTrigger(_R1AbilityHash);
        _Animator.ResetTrigger(_R2AbilityHash);
        // Note: We deliberately don't reset OnDefeat here.
        // Death shouldn't be cancellable.
    }
}