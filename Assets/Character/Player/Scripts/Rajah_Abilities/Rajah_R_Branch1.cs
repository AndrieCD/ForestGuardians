// Rajah_R_Branch1.cs
// [R] Sovereign's Wrath — Branch 1 of Rajah Bagwis's ultimate.
//
// PASSIVE (always active after branch is selected):
//   - LMB attacks gain lifesteal — each Primary hit heals Rajah for a % of damage dealt
//   - Increased base damage — adds a flat ATK bonus to Rajah's StatBlock permanently
//
// ACTIVE (triggered by pressing R):
//   - Phase 1: Rajah becomes untargetable for a short window
//   - Phase 2: Slash all enemies in a wide arc in front
//   - Phase 3: Final high-damage single strike
//
// CURRENT STATE: Stubs only. Passive and active logic marked with TODO.
// Passive hooks (OnEquip/OnUnequip) and active structure are scaffolded correctly.
//
// Inspector setup: Assign the RajahBranch1_SovereignsWrath SO_Ability asset
// to SO_Guardian.AbilityR_Branch1 — this class reads cooldown and scaling from it.

using System.Collections;
using UnityEngine;

public class Rajah_R_Branch1 : Sc_BaseAbility
{
    // Passive modifier applied permanently on equip — removed on unequip.
    // Holds the flat ATK bonus from "Increased base damage".
    // Stored so we can remove exactly this modifier on OnUnequip.
    private Sc_Modifier _passiveModifier;


    public Rajah_R_Branch1(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        // PASSIVE — applied once when the branch is selected, lasts the whole stage

        // TODO: Apply flat ATK bonus ("Increased base damage") using BuildModifier()
        // Example when values are ready:
        //   float bonusATK = _AbilityData.GetStat("BonusBaseDamage", CurrentLevel, user.Stats.AttackPower.GetValue());
        //   _passiveModifier = BuildModifier("Sovereign's Wrath — Passive ATK",
        //       ModifierSource.Ability,
        //       new Sc_StatEffect(StatType.AttackPower, bonusATK, StatModType.Flat));
        //   ApplyToSelf(user, _passiveModifier);

        // TODO: Subscribe to an OnPrimaryHit event to apply lifesteal per hit.
        // Lifesteal on hit pattern:
        //   1. Primary fires → event fires with damage dealt as payload
        //   2. This handler heals user for (damage * lifestealPercent)
        // Requires Rajah_Primary to expose: public static event Action<float> OnPrimaryHit
        // (firing the actual damage dealt, after crit, as the payload)

        Debug.Log($"[{user.name}] Sovereign's Wrath equipped — passive active.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        // Remove the passive ATK modifier cleanly
        if (_passiveModifier != null)
        {
            user.Stats.RemoveModifier(_passiveModifier);
            _passiveModifier = null;
        }

        // TODO: Unsubscribe from OnPrimaryHit when implemented

        Debug.Log($"[{user.name}] Sovereign's Wrath unequipped.");
    }


    // -------------------------------------------------------------------------
    // Active
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        TriggerAbilityAnimation(user);
        user.StartCoroutine(SovereignsWrathRoutine(user));

        StartCooldown(user, GetAbilityCooldown(user));
    }


    // Sequences through the three phases of the active ability.
    // Each phase is separated so timing, VFX, and hit detection
    // can be added independently without restructuring the flow.
    private IEnumerator SovereignsWrathRoutine(Mb_CharacterBase user)
    {
        // --- Phase 1: Become untargetable ---
        // TODO: Disable the player's hitbox / collision layer so enemies
        //       cannot deal damage during the slash window.
        //       Suggested approach: toggle a dedicated "Untargetable" layer or
        //       set a flag on Mb_HealthComponent that causes TakeDamage() to return early.
        Debug.Log("[Sovereign's Wrath] Phase 1 — Untargetable.");

        float untargetableDuration = 1.0f; // TODO: move to SO scaling
        yield return new WaitForSeconds(untargetableDuration);


        // --- Phase 2: Wide arc slash hitting all enemies in front ---
        // TODO: OverlapSphere or OverlapBox in front of Rajah, damage all CuBots hit.
        //       Use ApplyToEnemy() for any debuffs applied during the slash.
        //       Damage formula: _AbilityData.GetStat("SlashDamage", CurrentLevel, user.Stats.AttackPower.GetValue())
        Debug.Log("[Sovereign's Wrath] Phase 2 — Arc slash.");

        PerformArcSlash(user);
        yield return new WaitForSeconds(0.3f); // Brief pause between phases


        // --- Phase 3: Final high-damage single strike ---
        // TODO: Single target or small area high-damage hit.
        //       Damage formula: _AbilityData.GetStat("FinalStrike", CurrentLevel, user.Stats.AttackPower.GetValue())
        Debug.Log("[Sovereign's Wrath] Phase 3 — Final strike.");

        PerformFinalStrike(user);


        // --- End: Restore targetable state ---
        // TODO: Re-enable hitbox / collision layer here
        Debug.Log("[Sovereign's Wrath] Complete — targetable restored.");
    }


    private void PerformArcSlash(Mb_CharacterBase user)
    {
        // TODO: Implement wide arc hit detection
        // Suggested: OverlapSphere centered slightly in front, large radius
        // All CuBots hit receive SlashDamage scaled from the SO

        // TODO: Remove this placeholder log when implemented
        Debug.Log("[Sovereign's Wrath] Arc slash placeholder — no damage yet.");
    }


    private void PerformFinalStrike(Mb_CharacterBase user)
    {
        // TODO: Implement final strike hit detection
        // Suggested: smaller OverlapSphere directly in front, high ATK scaling

        // TODO: Remove this placeholder log when implemented
        Debug.Log("[Sovereign's Wrath] Final strike placeholder — no damage yet.");
    }


    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        // TODO: Add TriggerRAbility() to Mb_GuardianAnimator when animation is ready
        // if (user is Mb_GuardianBase guardian)
        //     guardian.GuardianAnimator?.TriggerRAbility();
    }
}