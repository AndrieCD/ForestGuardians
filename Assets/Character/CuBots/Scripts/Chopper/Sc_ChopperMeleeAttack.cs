using System.Collections;
using UnityEngine;

/// <summary>
/// Chopper's melee attack — a short-range axe slash.
///
/// WINDUP FLOW:
///   1. CheckCooldown() — bail if still on CD
///   2. Signal controller to freeze movement (via callback)
///   3. Wait WindupDuration seconds (animation plays)
///   4. Apply OverlapSphere damage hit
///   5. Signal controller to resume movement
///   6. Start cooldown
/// </summary>
public class Sc_ChopperMeleeAttack : Sc_BaseAbility
{
    private const float _AttackRange = 1.8f;

    // How long Chopper pauses before the hit lands — tweak to match animation
    private const float WINDUP_DURATION = 0.5f;

    // Radius of the overlap sphere used to find hit targets
    private const float ATTACK_RADIUS = 1.0f;

    // Called when windup starts so the controller can freeze movement
    private System.Action _onWindupStart;

    // Called when the hit lands so the controller can resume movement
    private System.Action _onWindupEnd;

    public Sc_ChopperMeleeAttack(
        SO_Ability abilityData,
        Mb_CharacterBase user,
        System.Action onWindupStart,
        System.Action onWindupEnd)
        : base(abilityData, user)
    {
        _onWindupStart = onWindupStart;
        _onWindupEnd = onWindupEnd;
    }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        // Start cooldown immediately — Chopper can't chain attacks during windup
        StartCooldown(user, GetAttackCooldown(user));

        // Run the windup → hit → resume sequence as a coroutine on the user MonoBehaviour
        user.StartCoroutine(WindupAndStrike(user));
    }

    private IEnumerator WindupAndStrike(Mb_CharacterBase user)
    {
        // Tell the controller to stop moving — windup begins
        _onWindupStart?.Invoke();

        // TODO: Trigger attack windup animation here via user's Animator

        yield return new WaitForSeconds(WINDUP_DURATION);

        // Apply the hit
        ApplyHit(user);

        // Tell the controller movement can resume
        _onWindupEnd?.Invoke();
    }

    private void ApplyHit(Mb_CharacterBase user)
    {
        // The slash origin is slightly in front of Chopper
        Vector3 slashCenter = user.transform.position + (Vector3.up * 1.0f) + (user.transform.forward * _AttackRange);

        Collider[] hits = Physics.OverlapSphere(slashCenter, ATTACK_RADIUS);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == user.gameObject) continue; // Never hit yourself
            if (hit.gameObject.CompareTag("CuBot")) continue; // CuBots don't hurt each other

            I_Damageable damageable = hit.GetComponent<I_Damageable>();
            if (damageable != null)
            {
                float damage = _AbilityData.GetStat(
                    "Damage",
                    CurrentLevel,
                    user.Stats.AttackPower.GetValue()
                );

                damage = ApplyCriticalStrike(damage, user);
                damageable.TakeDamage(damage);
                break; // Single target — stop after first valid hit
            }
        }
    }
}