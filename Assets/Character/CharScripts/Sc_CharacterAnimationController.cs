using UnityEngine;

public class Sc_CharacterAnimationController : MonoBehaviour
{
    private Animator _Animator;


    // Enums
    private CharacterState _CurrentState;
    private AttackType _CurrentAttackType;

    // Animator parameter hashes (performance safe)
    private static readonly int StateHash = Animator.StringToHash("State");
    private static readonly int AttackHash = Animator.StringToHash("AttackType");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

    private void Awake( )
    {
        _Animator = GetComponent<Animator>( );
    }

    public void SetMovementSpeed(float speed)
    {
        _Animator.SetFloat(SpeedHash, speed);
    }

    public void SetGrounded(bool grounded)
    {
        _Animator.SetBool(IsGroundedHash, grounded);
    }

    public void SetState(CharacterState newState)
    {
        if (_CurrentState == newState) return;

        _CurrentState = newState;
        _Animator.SetInteger(StateHash, (int)newState);
    }

    public void PlayAttack(AttackType attackType)
    {
        _CurrentAttackType = attackType;
        _Animator.SetInteger(AttackHash, (int)attackType);
        _Animator.SetTrigger("AttackTrigger");
    }

    public void PlayHit( )
    {
        _Animator.SetTrigger("HitTrigger");
    }

    public void PlayDeath( )
    {
        SetState(CharacterState.Dead);
        _Animator.SetTrigger("DeathTrigger");
    }

}