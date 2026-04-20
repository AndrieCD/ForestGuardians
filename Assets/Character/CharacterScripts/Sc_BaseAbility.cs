// Sc_BaseAbility.cs
// The base class for every ability in the game — for both Guardians and CuBots.
//
// WHAT THIS CLASS PROVIDES TO ALL DERIVED ABILITIES:
//
//   LIFECYCLE
//     OnEquip()    — called once when the ability is assigned to a character
//     Activate()   — called when the player (or AI) triggers the ability
//     OnUnequip()  — called on death or scene cleanup
//
//   COOLDOWN (two separate paths — DO NOT mix them up)
//     GetAbilityCooldown(user)  — for Q / E / R abilities: base cooldown reduced by Haste
//     GetAttackCooldown(user)   — for Primary / Secondary: derived from AttackSpeed
//     CheckCooldown()           — returns false if still on cooldown
//     StartCooldown(user)       — starts the cooldown timer coroutine
//     GetRemainingCooldown()       — read by UI to display cooldown timers
//
//   UPGRADES
//     LevelUp()         — increments the ability level, calls OnLevelUp() hook
//     CurrentLevel      — read by UI and RewardsManager
//     MaxLevel          — sourced from SO_Ability.MaxLevel
//
//   DAMAGE HELPERS
//     ApplyCriticalStrike()  — rolls crit and returns modified damage
//
//   MODIFIER HELPERS (the key reusable methods)
//     ApplyToSelf(user, modifier)    — apply a stat modifier to the ability's user
//     ApplyToEnemy(target, modifier) — apply a stat modifier to an enemy CuBot
//     BuildModifier(...)             — convenience builder, avoids repeating boilerplate
//
//   ANIMATION
//     TriggerAbilityAnimation(user)  — virtual hook, override per ability

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Sc_BaseAbility
{
    // The ScriptableObject that holds all data for this ability
    protected SO_Ability _AbilityData;

    // The character that owns this ability — set in the constructor
    protected Mb_CharacterBase _User;

    // Tracks how long until the ability can be used again
    protected float _CooldownRemaining = 0f;

    // Current upgrade level — starts at 1, caps at SO_Ability.MaxLevel
    public int CurrentLevel { get; private set; } = 1;

    // Read directly from the SO so there's one source of truth
    public int MaxLevel => _AbilityData != null ? _AbilityData.MaxLevel : 1;

    // lets RewardsManager read SO identity data without exposing the full protected field publicly.
    public SO_Ability GetAbilityData() => _AbilityData;

    // EVENTS for UI

    // event fired when cooldown changes, so UI can update timers
    public event Action<float> OnCooldownChanged;



    // constructor

    protected Sc_BaseAbility(SO_Ability abilityData, Mb_CharacterBase user)
    {
        _AbilityData = abilityData;
        _User = user;
    }


    // -------------------------------------------------------------------------
    // Lifecycle — override in derived classes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called once when this ability is assigned to a character.
    /// Use for passive setup, event subscriptions, caching references.
    /// </summary>
    public virtual void OnEquip(Mb_CharacterBase user) { }

    /// <summary>
    /// Called when the player or AI activates this ability.
    /// Always call CheckCooldown() at the top and StartCooldown() at the end.
    /// </summary>
    public virtual void Activate(Mb_CharacterBase user) { }

    /// <summary>
    /// Called on character death or scene cleanup.
    /// Use for passive teardown and unsubscribing from events.
    /// </summary>
    public virtual void OnUnequip(Mb_CharacterBase user) { }


    // -------------------------------------------------------------------------
    // Upgrade System
    // -------------------------------------------------------------------------

    /// <summary>
    /// Increments the ability level by 1, up to MaxLevel.
    /// Called by Mb_RewardsManager when the player picks an ability upgrade.
    /// Fires OnLevelUp() so derived classes can react (e.g. recaching values).
    /// </summary>
    public void LevelUp()
    {
        if (CurrentLevel >= MaxLevel)
        {
            Debug.LogWarning($"[{_AbilityData.AbilityName}] Already at max level ({MaxLevel}). Cannot level up further.");
            return;
        }

        CurrentLevel++;
        OnLevelUp();

        Debug.Log($"[{_AbilityData.AbilityName}] Leveled up to {CurrentLevel}/{MaxLevel}.");
    }

    /// <summary>
    /// Override this in derived classes to react when the ability levels up —
    /// e.g. recalculate a cached damage value or refresh a duration.
    /// The base implementation does nothing.
    /// </summary>
    protected virtual void OnLevelUp() { }


    // -------------------------------------------------------------------------
    // Cooldown — two separate paths
    // -------------------------------------------------------------------------

    /// <summary>
    /// For Q / E / R abilities.
    /// Returns the effective cooldown after applying the user's Haste stat.
    /// Formula: effectiveCooldown = baseCooldown / (1 + Haste / 100)
    /// A Haste of 100 halves the cooldown; a Haste of 0 leaves it unchanged.
    /// </summary>
    protected float GetAbilityCooldown(Mb_CharacterBase user)
    {
        // TODO: Replace 0f with user.Stats.Haste.Value() once Haste is added to Mb_StatBlock
        float haste = user.Stats.Haste.GetValue();
        return _AbilityData.Cooldown / (1f + haste / 100f);
    }

    /// <summary>
    /// For Primary / Secondary (basic attacks).
    /// Returns the time between attacks derived from AttackSpeed.
    /// Formula: attackInterval = 1 / AttackSpeed
    /// </summary>
    protected float GetAttackCooldown(Mb_CharacterBase user)
    {
        float attackSpeed = user.Stats.AttackSpeed.GetValue();

        // Guard against division by zero if AttackSpeed is somehow 0
        if (attackSpeed <= 0f) return 1f;

        return 1f / attackSpeed;
    }

    /// <summary>
    /// Returns true if the ability is ready to fire.
    /// Call this at the top of Activate() and return early if false.
    /// </summary>
    protected bool CheckCooldown()
    {
        return _CooldownRemaining <= 0f;
    }

    /// <summary>
    /// Starts the cooldown timer. Call this at the end of Activate() after
    /// all effects have been applied. Pass the correct cooldown duration —
    /// either GetAbilityCooldown() or GetAttackCooldown() depending on ability type.
    /// </summary>
    protected void StartCooldown(MonoBehaviour runner, float cooldownDuration)
    {
        _CooldownRemaining = cooldownDuration;
        runner.StartCoroutine(TickCooldown());
    }

    // Counts down _CooldownRemaining in 0.1s steps until it hits zero.
    private IEnumerator TickCooldown()
    {
        while (_CooldownRemaining > 0f)
        {
            yield return new WaitForSeconds(0.1f);
            _CooldownRemaining -= 0.1f;
            OnCooldownChanged?.Invoke(_CooldownRemaining);
        }
        _CooldownRemaining = 0f;
        OnCooldownChanged?.Invoke(_CooldownRemaining);
    }

    /// <summary>
    /// Returns the remaining cooldown of the ability (float) for UI display.
    /// </summary>
    /// <returns></returns>
    public float GetRemainingCooldown()
    {
        return _CooldownRemaining;
    }

    // -------------------------------------------------------------------------
    // Damage Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rolls against the user's CriticalChance and applies CriticalDamage if it hits.
    /// Returns the final damage value — either normal or crit-multiplied.
    /// CriticalChance is stored as a percentage (e.g. 15 = 15%).
    /// CriticalDamage is stored as a percentage multiplier (e.g. 150 = 150% = 1.5x).
    /// </summary>
    protected float ApplyCriticalStrike(float baseDamage, Mb_CharacterBase user)
    {
        float critChance = user.Stats.CriticalChance.GetValue() / 100f;
        float roll = UnityEngine.Random.value; // 0.0 to 1.0

        if (roll <= critChance)
        {
            float critMultiplier = user.Stats.CriticalDamage.GetValue() / 100f;
            Debug.Log($"[{_AbilityData.AbilityName}] Critical Strike! {critMultiplier}x damage.");
            return baseDamage * critMultiplier;
        }

        return baseDamage;
    }


    // -------------------------------------------------------------------------
    // Modifier Helpers — the main reusable methods for applying stat changes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Convenience builder — wraps effects into a named Sc_Modifier so derived
    /// classes don't repeat the same boilerplate every time.
    ///
    /// Usage example:
    ///   var mod = BuildModifier("Sky Rend Shield", ModifierSource.Ability, 4f,
    ///       new Sc_StatEffect(StatType.Shielding, 200f, StatModType.Flat));
    /// </summary>
    protected Sc_Modifier BuildModifier(
        string name,
        ModifierSource source,
        float duration,
        params Sc_StatEffect[] effects)
    {
        // params lets callers pass effects inline without building a list manually
        return new Sc_Modifier(name, source, new List<Sc_StatEffect>(effects), duration);
    }

    /// <summary>
    /// Overload for permanent modifiers (no duration needed).
    /// duration defaults to float.PositiveInfinity inside Sc_Modifier already,
    /// but this overload makes intent explicit at the call site.
    /// </summary>
    protected Sc_Modifier BuildModifier(
        string name,
        ModifierSource source,
        params Sc_StatEffect[] effects)
    {
        return new Sc_Modifier(name, source, new List<Sc_StatEffect>(effects));
    }

    /// <summary>
    /// Applies a stat modifier to the ability's own user (self-buff, self-debuff, shield, etc.).
    /// All stat changes must go through this — never touch Sc_Stat.Effects directly.
    /// </summary>
    protected void ApplyToSelf(Mb_CharacterBase user, Sc_Modifier modifier)
    {
        user.Stats.AddModifier(modifier);
    }

    /// <summary>
    /// Applies a stat modifier to an enemy CuBot (debuffs, slows, burns, etc.).
    /// All stat changes must go through this — never touch Sc_Stat.Effects directly.
    /// </summary>
    protected void ApplyToEnemy(MB_CuBotBase target, Sc_Modifier modifier)
    {
        target.Stats.AddModifier(modifier);
    }


    // -------------------------------------------------------------------------
    // Animation Hook
    // -------------------------------------------------------------------------

    /// <summary>
    /// Override in each ability to trigger the correct animator state.
    /// Called from Activate() — base implementation does nothing.
    /// Keeping this separate from Activate() keeps activation logic clean.
    /// </summary>
    protected virtual void TriggerAbilityAnimation(Mb_CharacterBase user) { }
}