// Rajah_Q_Ability.cs
// [Q] Sky Rend — Rajah dashes forward, damaging all enemies he passes through.
// Each enemy hit adds additional shielding on top of the base shield.
// Shield value and damage both scale with ability level via SO_Ability.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rajah_Q_Ability : Sc_BaseAbility
{
    // Dash movement values — tuned here, not in the SO (not level-scaled)
    private const float DASH_SPEED = 50f;
    private const float DASH_DURATION = 0.25f;

    // Overlap sphere radius while dashing — adjust to match Rajah's character width
    private const float HIT_RADIUS = 1.2f;

    // How long the shield lasts after being granted
    private const float SHIELD_DURATION = 4f;

    // Each additional enemy hit beyond the first adds this fraction of the base shield
    private const float SHIELD_PER_ENEMY_FRACTION = 0.25f;

    // Tracks enemies already hit this dash so we don't damage them twice
    private readonly HashSet<Collider> _hitThisDash = new HashSet<Collider>();

    private Camera _cam;

    private Mb_HealthComponent _HealthComponent;

    public Rajah_Q_Ability(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
        _cam = Camera.main;
    }


    public override void OnEquip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Equipped {_AbilityData.AbilityName}.");

        _HealthComponent = user.GetComponent<Mb_HealthComponent>();
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Unequipped {_AbilityData.AbilityName}.");
    }


    // Called when the player presses [Q]
    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        TriggerAbilityAnimation(user);

        // Flatten camera forward to XZ plane so the dash is always horizontal
        Vector3 dashDir = _cam.transform.forward;
        dashDir.y = 0f;
        dashDir.Normalize();

        // Fallback to character's own forward if camera direction collapses
        if (dashDir == Vector3.zero)
            dashDir = user.transform.forward;

        user.Movement.StartDash(dashDir * DASH_SPEED, DASH_DURATION);

        // Hit detection runs in a coroutine because it needs to poll every frame
        user.StartCoroutine(DashHitRoutine(user));

        // Q is an ability, so cooldown is reduced by Haste
        StartCooldown(user, GetAbilityCooldown(user));
    }


    // Polls for enemy hits every frame during the dash.
    // After the dash ends, calculates and applies the earned shield.
    private IEnumerator DashHitRoutine(Mb_CharacterBase user)
    {
        ApplyBaseDashShield(user);

        _hitThisDash.Clear();

        float elapsed = 0f;

        while (elapsed < DASH_DURATION)
        {
            Collider[] nearby = Physics.OverlapSphere(user.transform.position, HIT_RADIUS);

            foreach (Collider col in nearby)
            {
                if (_hitThisDash.Contains(col)) continue;

                MB_CuBotBase cuBot = col.GetComponent<MB_CuBotBase>();
                if (cuBot == null) continue;

                // Damage scales with ATK and AP at current ability level — all values from SO
                float damage = _AbilityData.GetStat(
                    "Damage",
                    CurrentLevel,
                    user.Stats.AttackPower.GetValue(),
                    user.Stats.AbilityPower.GetValue()
                );

                cuBot.Health.TakeDamage(damage);
                _hitThisDash.Add(col);

                Debug.Log($"[Sky Rend] Hit {col.name} for {damage} damage.");
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyPostDashShield(user, _hitThisDash.Count);
    }


    // Calculates and applies the shield earned from the dash.
    // Shield scales with enemies hit, ability level, and AP — all from the SO.
    private void ApplyPostDashShield(Mb_CharacterBase user, int enemiesHit)
    {
        if (enemiesHit == 0) return;

        var health = user.GetComponent<Mb_HealthComponent>();

        float baseShield = _AbilityData.GetStat(
            "Shield",
            CurrentLevel,
            0f,
            user.Stats.AbilityPower.GetValue()
        );

        float bonusShield = baseShield * (enemiesHit * SHIELD_PER_ENEMY_FRACTION);

        health.AddShield(bonusShield, SHIELD_DURATION);

        Debug.Log($"[Sky Rend] Granted {bonusShield} shield from {enemiesHit} enemies hit.");
    }

    // Default base shield, granted upon pressing Q even if no enemies are hit. Scales with level and AP but is unaffected by enemy hits.
    private void ApplyBaseDashShield(Mb_CharacterBase user)
    {
        var health = user.GetComponent<Mb_HealthComponent>();

        float baseShield = _AbilityData.GetStat(
            "Shield",
            CurrentLevel,
            0f,
            user.Stats.AbilityPower.GetValue()
        );

        health.AddShield(baseShield, SHIELD_DURATION);

        Debug.Log($"[Sky Rend] Granted base shield of {baseShield}.");
    }


    // Fires the Q ability animation on the guardian's animator
    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerQAbility();
    }
}