// Mb_BernieController.cs
// Bernie — fire-themed mini-boss CuBot.
// High HP, slow movement, devastating short-range fire cone.
// Represents slash-and-burn (kaingin) farming practices.
//
// Inherits targeting, movement, aggro switching, and pool reset from
// Mb_CuBotController — no special movement behavior needed.
//
// Inspector setup:
//   - Assign SO_CuBots template (inherited field)
//   - Assign _FirePoint — a child Transform at the flamethrower nozzle
//   - NavMeshAgent must be on the same GameObject
//   - Mb_StatusEffectController must be on target characters for Burn to apply
//   - Animator / Mb_CuBotAnimator optional but expected for visual feedback
//   - SO_Ability asset needs three scaling entries:
//       "Damage"       — flat + ATK scaling
//       "BurnDamage"   — flat + ATK scaling (Haste multiplier applied at cast time)
//       "BurnDuration" — flat seconds only

using UnityEngine;

public class Mb_BernieController : Mb_CuBotController
{
    [Header("Bernie Settings")]
    [Tooltip("Child Transform at the tip of Bernie's flamethrower nozzle. " +
             "Cone VFX and SFX play here. Position it at roughly chest/weapon height.")]
    [SerializeField] private UnityEngine.Transform _FirePoint;


    // -------------------------------------------------------------------------
    // Ability Setup
    // -------------------------------------------------------------------------

    protected override void AssignAbilities()
    {
        if (_CuBotTemplate.PrimaryAttack == null)
        {
            UnityEngine.Debug.LogError("[Mb_BernieController] PrimaryAttack SO_Ability " +
                                       "is not assigned on the SO_CuBots template.");
            return;
        }
         
        Abilities.SetPrimarySlot(new Sc_BernieFireCone(
            _CuBotTemplate.PrimaryAttack,
            this,
            firePoint: _FirePoint
        ));
    }


    // -------------------------------------------------------------------------
    // Attack Range Behavior
    // -------------------------------------------------------------------------

    protected override void OnInAttackRange()
    {
        // Face the current target before firing so the cone is correctly oriented
        if (_CurrentTarget != null)
            transform.LookAt(_CurrentTarget);

        StartCoroutine(MeleeAttackCoroutine());

    }


    // -------------------------------------------------------------------------
    // Animator
    // -------------------------------------------------------------------------

    protected override void UpdateAnimator()
    {
        if (_BasicCuBotAnimator == null) return;

        // Drive locomotion blend — speed normalised against move speed stat
        // so the blend tree threshold of 1.0 = full run regardless of actual speed value
        float moveSpeed = Stats != null && Stats.MoveSpeed != null
            ? Stats.MoveSpeed.GetValue()
            : _CuBotTemplate.MoveSpeed;

        float normalizedSpeed = moveSpeed > 0f
            ? _Agent.velocity.magnitude / moveSpeed
            : _Agent.velocity.magnitude;

        _BasicCuBotAnimator.SetSpeed(normalizedSpeed);

        // TODO: Trigger fire cone animation once Bernie's Animator Controller is set up.
        // Suggested pattern — call from Sc_BernieFireCone.Activate() via a callback,
        // same approach as Sc_ChopperMeleeAttack:
        //   user.GetComponent<Mb_CuBotAnimator>()?.TriggerAttack();
    }


    // -------------------------------------------------------------------------
    // Pool Reset
    // -------------------------------------------------------------------------

    protected override void OnControllerReset()
    {
        // No per-instance state to reset beyond what Mb_CuBotController handles.
        // Burn DoT on targets is managed by their own Mb_StatusEffectController
        // and clears itself via OnEnable() pool safety — no action needed here.
    }

}
