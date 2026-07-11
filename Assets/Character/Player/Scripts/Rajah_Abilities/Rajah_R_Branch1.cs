using System;
using System.Collections;
using UnityEngine;

public class Rajah_R_Branch1 : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // CONFIG (non-SO timing values)
    // -------------------------------------------------------------------------

    private const int TICK_COUNT = 8;
    private const float TICK_INTERVAL = 0.25f;
    private const float FINAL_DELAY = 0.5f;

    private const float HIT_RADIUS = 5f;
    private const float HIT_OFFSET = 5f;

    // -------------------------------------------------------------------------
    // PASSIVE STATE
    // -------------------------------------------------------------------------

    private Sc_Modifier _atkModifier;
    private float _lifesteal; // decimal (0.5 = 50%)

    // Cached components
    private Mb_HealthComponent _health;
    // -------------------------------------------------------------------------
    // CONSTRUCTOR
    // -------------------------------------------------------------------------

    public Rajah_R_Branch1(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
    }

    // -------------------------------------------------------------------------
    // LIFECYCLE
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        _health = user.GetComponent<Mb_HealthComponent>();

        // --- Passive: Attack Power Modifier ---
        float bonusATK = _AbilityData.GetStat(
            "BonusBaseDamage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue()
        );

        _atkModifier = BuildModifier(
            "Sovereign's Wrath — ATK Bonus",
            ModifierSource.Ability,
            new Sc_StatEffect(StatType.AttackPower, bonusATK, StatModType.Percent)
        );

        ApplyToSelf(user, _atkModifier);

        // --- Passive: Lifesteal ---
        _lifesteal = _AbilityData.GetStat("Lifesteal", CurrentLevel);

        Rajah_Primary.OnPrimaryDamageDealt += HandlePrimaryDamageDealt;

        Debug.Log($"Sovereign's Wrath equipped.");
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        if (_atkModifier != null)
        {
            user.Stats.RemoveModifier(_atkModifier);
            _atkModifier = null;
        }

        Rajah_Primary.OnPrimaryDamageDealt -= HandlePrimaryDamageDealt;

        Debug.Log($"Sovereign's Wrath unequipped.");
    }

    // -------------------------------------------------------------------------
    // PASSIVE: LIFESTEAL
    // -------------------------------------------------------------------------

    private void HandlePrimaryDamageDealt(float damage, Mb_CharacterBase source)
    {
        if (source != _User) return;
        if (_health == null) return;

        float healAmount = damage * _lifesteal;
        _health.Heal(healAmount);
    }

    // -------------------------------------------------------------------------
    // ACTIVE
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        TriggerAbilityAnimation(user);

        user.StartCoroutine(SovereignsWrathRoutine(user));

        StartCooldown(user, GetAbilityCooldown(user));

    }

    private IEnumerator SovereignsWrathRoutine(Mb_CharacterBase user)
    {
        // --- Setup ---
        SetInvulnerable(true);
        var controller = user as Mb_PlayerController;
        controller?.AddDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );

        // --- Tick Loop ---
        for (int i = 0; i < TICK_COUNT; i++)
        {
            PerformAoEHit(user, isFinal: false);

            yield return new WaitForSeconds(TICK_INTERVAL);
        }

        // --- Final Delay ---
        yield return new WaitForSeconds(FINAL_DELAY);

        // --- Final Strike ---
        PerformAoEHit(user, isFinal: true);

        // --- Cleanup ---
        SetInvulnerable(false);
        controller?.RemoveDisable(ActionDisableFlags.AllAbilities | ActionDisableFlags.AllAttacks);

        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.EndR1Ability();
    }

    // -------------------------------------------------------------------------
    // DAMAGE LOGIC
    // -------------------------------------------------------------------------

    private void PerformAoEHit(Mb_CharacterBase user, bool isFinal)
    {
        Vector3 forward = GetCurrentForwardDirection(user);
        Vector3 center = user.transform.position + forward * HIT_OFFSET;

        Collider[] hits = Physics.OverlapSphere(center, HIT_RADIUS);

        // Play SFX
        Mb_AudioManager.PlaySFX(CombatSFX.Rajah_Primary);

        if (isFinal)
        {
            // Play VFX (Rajah_Primary_Cast) 4x at random spots within the range
            for (int j = 0; j < 16; j++)
            {
                Vector3 randomPosition = center + UnityEngine.Random.insideUnitSphere * HIT_RADIUS;

                Mb_VFXManager.Play(
                    VFXType.Rajah_Primary_Cast,
                    randomPosition,
                    Quaternion.identity
                );
            }
        } else
        {
            // Play VFX (Rajah_Primary_Cast) 4x at random spots within the range
            for (int j = 0; j < 8; j++)
            {
                Vector3 randomPosition = center + UnityEngine.Random.insideUnitSphere * HIT_RADIUS;

                Mb_VFXManager.Play(
                    VFXType.Rajah_Primary_Cast,
                    randomPosition,
                    Quaternion.identity
                );
            }
        }


        float damage = isFinal
            ? _AbilityData.GetStat("Damage", CurrentLevel, user.Stats.AttackPower.GetValue()) * 3f
            : _AbilityData.GetStat("Damage", CurrentLevel, user.Stats.AttackPower.GetValue());

        foreach (Collider col in hits)
        {
            MB_CuBotBase enemy = col.GetComponent<MB_CuBotBase>();
            if (enemy == null) continue;

            enemy.Health.TakeDamage(damage);

            float healAmount = damage * _lifesteal;
            _health.Heal(healAmount);
        }
    }

    // -------------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------------

    private Vector3 GetCurrentForwardDirection(Mb_CharacterBase user)
    {
        Camera cam = Camera.main;
        Vector3 direction = cam != null
            ? cam.transform.forward
            : user.transform.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return user.transform.forward;

        return direction.normalized;
    }

    // -------------------------------------------------------------------------
    // SYSTEM STUBS (IMPLEMENT OR CONNECT)
    // -------------------------------------------------------------------------

    private void SetInvulnerable(bool state)
    {
        if (_health == null) return;

        _health.IsInvulnerable = state;
    }

    //private void SetMovementEnabled(Mb_CharacterBase user, bool enabled)
    //{
    //    if (user.Movement != null)
    //        user.Movement.SetMovementEnabled(enabled);

    //    // OPTIONAL:
    //    // user.AbilityController?.SetAbilitiesEnabled(enabled);
    //}

    // -------------------------------------------------------------------------
    // ANIMATION
    // -------------------------------------------------------------------------

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerR1Ability(); // stub
    }
}
