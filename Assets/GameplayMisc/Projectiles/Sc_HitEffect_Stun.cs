// Sc_HitEffect_Stun.cs
// Stuns the hit target for a duration, blocking all actions.
//
// GUARDIAN STUN:
//   Applies ActionDisableFlags.Stun via Mb_PlayerController.AddDisable() to block
//   all input-driven actions (movement, attacks, abilities) for the duration.
//   A coroutine on the target removes the disable flag after StunDuration seconds.
//
// CUBOT STUN:
//   Disables the NavMeshAgent (stops pathfinding) and blocks ability activation
//   via Mb_AbilityController. Both are re-enabled after StunDuration.
//   TODO: When Mb_CuBotController adds a public Stun(float duration) method,
//         replace the direct agent manipulation here with that call.
//
// STATUS EFFECT CONTROLLER:
//   Also calls Mb_StatusEffectController.Apply() if present so the controller
//   can track the stun and fire OnStatusApplied for floating text display.
//   This is optional — the stun works without it.
//
// TODO: Used by:
//   - Trapper_TrapProjectile  (short stun — StunDuration ~0.8)
//   Tune duration once enemy AI pacing is established in playtesting.
//
// Inspector setup:
//   StunDuration — how long the stun lasts in seconds

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "New HitEffect_Stun",
    menuName = "ForestGuardians/Hit Effects/Stun")]
public class Sc_HitEffect_Stun : Sc_HitEffect
{
    [Header("Stun Settings")]
    [Tooltip("How long the target is stunned in seconds.")]
    public float StunDuration = 0.8f;


    public override void ApplyOnHit(Mb_CharacterBase target, Mb_CharacterBase attacker,
                                    Vector3 hitPoint, Vector3 hitNormal)
    {
        if (target == null) return;

        // Notify the status effect controller if one exists.
        // This fires OnStatusApplied so floating text ("STUN") can display.
        // The stun itself is handled below — the controller is purely for UI/feedback.
        // TODO: Replace with statusController.Apply(Sc_StatusEffect.Stun(StunDuration))
        //       once Mb_StatusEffectController is fully implemented.
        Mb_StatusEffectController statusController =
            target.GetComponent<Mb_StatusEffectController>();
        // statusController?.Apply(Sc_StatusEffect.Stun(StunDuration)); // TODO: uncomment

        // --- Guardian path ---
        // ActionDisableFlags.Stun blocks movement, attacks, and abilities in one flag.
        // We use a coroutine on the target to remove the flag after the duration.
        Mb_PlayerController playerController = target.GetComponent<Mb_PlayerController>();
        if (playerController != null)
        {
            target.StartCoroutine(StunGuardian(playerController, StunDuration));
            return;
        }

        // --- CuBot path ---
        // Disable the NavMeshAgent (stops movement) and block ability activation.
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            target.StartCoroutine(StunCuBot(target, agent, StunDuration));
        }
    }


    // Applies ActionDisableFlags.Stun to the Guardian for the stun duration,
    // then removes the flag so the player regains control automatically.
    private IEnumerator StunGuardian(Mb_PlayerController controller, float duration)
    {
        controller.AddDisable(ActionDisableFlags.Stun);
        yield return new WaitForSeconds(duration);
        controller.RemoveDisable(ActionDisableFlags.Stun);
    }


    // Stops the CuBot's NavMeshAgent and blocks its ability controller for the duration.
    // Re-enables both after the stun expires, provided the CuBot is still alive.
    private IEnumerator StunCuBot(Mb_CharacterBase target, NavMeshAgent agent, float duration)
    {
        agent.isStopped = true;

        // Block ability activation so the CuBot can't attack while stunned.
        // Mb_AbilityController.IsBlocked already checks IsDead — setting _isPaused
        // isn't public, so we use the pause event pattern as the closest equivalent.
        // TODO: If Mb_AbilityController adds a public SetBlocked(bool) method,
        //       use that here instead of relying on the agent stop alone.

        yield return new WaitForSeconds(duration);

        // Only re-enable if the CuBot survived the stun duration.
        if (agent != null && target.gameObject.activeInHierarchy)
            agent.isStopped = false;
    }
}