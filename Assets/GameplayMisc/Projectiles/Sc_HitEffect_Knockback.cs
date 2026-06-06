// Sc_HitEffect_Knockback.cs
// Pushes the hit target away from the impact point.
//
// GUARDIAN KNOCKBACK:
//   Uses Mb_Movement.StartDash(velocity, duration) — this is the established
//   displacement API and correctly bypasses player input during the knockback window.
//
// CUBOT KNOCKBACK:
//   Disables the NavMeshAgent for the duration and moves the CuBot directly via
//   its CharacterController or transform. The agent is re-enabled after the duration.
//   TODO: If Mb_CuBotController adds a public ApplyKnockback(Vector3, float) method
//         in the future, replace the direct agent manipulation here with that call.
//         For now, direct manipulation is the safest approach without modifying
//         Mb_CuBotController.
//
// TODO: Used by:
//   - Hunter_Bolt  (light knockback — KnockbackForce ~8, KnockbackDuration ~0.2)
//   Tune values once movement feel is established in playtesting.
//
// Inspector setup:
//   KnockbackForce    — speed of the knockback displacement in units/second
//   KnockbackDuration — how long the knockback lasts in seconds

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "New HitEffect_Knockback",
    menuName = "ForestGuardians/Hit Effects/Knockback")]
public class Sc_HitEffect_Knockback : Sc_HitEffect
{
    [Header("Knockback Settings")]
    [Tooltip("Speed of the knockback push in world units per second.")]
    public float KnockbackForce = 8f;

    [Tooltip("How long the knockback displacement lasts in seconds.")]
    public float KnockbackDuration = 0.2f;


    public override void ApplyOnHit(Mb_CharacterBase target, Mb_CharacterBase attacker,
                                    Vector3 hitPoint, Vector3 hitNormal)
    {
        if (target == null) return;

        // Push direction: away from the hit surface using the surface normal.
        // If hitNormal is zero (e.g. no surface data), fall back to pushing away
        // from the attacker's position so the knockback still feels intentional.
        Vector3 pushDirection = hitNormal != Vector3.zero
            ? hitNormal.normalized
            : (target.transform.position - hitPoint).normalized;

        Vector3 knockbackVelocity = pushDirection * KnockbackForce;

        // --- Guardian path ---
        // Mb_Movement.StartDash() is the correct API for displacing a Guardian.
        // It bypasses normal movement input for the duration, which is exactly
        // what we want — the player shouldn't be able to counter-strafe a knockback.
        Mb_Movement movement = target.GetComponent<Mb_Movement>();
        if (movement != null)
        {
            movement.StartDash(knockbackVelocity, KnockbackDuration);
            return;
        }

        // --- CuBot path ---
        // Disable the NavMeshAgent so it stops pathfinding during the knockback,
        // then move the CuBot directly. A MonoBehaviour runner is needed to run
        // the re-enable coroutine — we use the target's own component for this.
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        CharacterController cc = target.GetComponent<CharacterController>();

        if (agent != null)
        {
            agent.enabled = false;

            // Run the re-enable and displacement on the target's MonoBehaviour
            // so the coroutine is tied to the CuBot's own lifecycle.
            target.StartCoroutine(
                ApplyCuBotKnockback(target, agent, cc, knockbackVelocity, KnockbackDuration)
            );
        }
    }


    // Moves the CuBot manually during the knockback window, then re-enables the agent.
    // Uses Time.deltaTime so the displacement respects game speed (including slow effects).
    private IEnumerator ApplyCuBotKnockback(
        Mb_CharacterBase target,
        NavMeshAgent agent,
        CharacterController cc,
        Vector3 velocity,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Prefer CharacterController.Move if available — it respects collision geometry.
            // Fall back to direct transform translation if no CharacterController is present.
            if (cc != null)
                cc.Move(velocity * Time.deltaTime);
            else
                target.transform.position += velocity * Time.deltaTime;

            yield return null;
        }

        // Re-enable the agent only if the CuBot is still alive and active.
        // If it died during knockback, the GameObject will be inactive and
        // the agent reference may be stale — the null check prevents a crash.
        if (agent != null && target.gameObject.activeInHierarchy)
            agent.enabled = true;
    }
}