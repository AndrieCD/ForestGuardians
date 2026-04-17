//// Sc_AugmentHandler.cs
//// Lives on the Guardian (held by Mb_GuardianBase, not a MonoBehaviour itself).
//// Responsible for:
////   1. Storing which augments the player has equipped (max 3 per stage).
////   2. Applying the stat effects of each augment onto the Guardian's stats.
////   3. Wiring up reactive behaviors (e.g., Primal Resonance stacking on ability cast).
////   4. Cleaning up listeners when the stage ends.

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class Sc_AugmentHandler
//{
//    // ── Constants ──────────────────────────────────────────────────────────────
//    public const int MAX_AUGMENTS = 3;

//    // ── State ──────────────────────────────────────────────────────────────────
//    private List<SO_Augment> _equippedAugments = new List<SO_Augment>();
//    private Mb_GuardianBase _guardian;      // The Guardian this handler belongs to
//    private MonoBehaviour _runner;          // Used to start coroutines (same GameObject)

//    // Stat dictionary — built from the Guardian's stats for use with Sc_Modifier
//    private Dictionary<StatType, Sc_Stat> _statDict;

//    // Tracks Sc_Modifiers we've created so we can remove them if needed
//    private List<Sc_Modifier> _appliedModifiers = new List<Sc_Modifier>();

//    // ── Reactive State ─────────────────────────────────────────────────────────
//    // Primal Resonance: tracks stacks this wave
//    private int _primalResonanceStacks = 0;
//    private List<Sc_StatEffect> _primalStackEffects = new List<Sc_StatEffect>();

//    // Fight or Flight: tracks which thresholds have been triggered to avoid re-applying
//    private bool _fofTier1Active = false;   // Below 40% HP buff
//    private bool _fofTier2Active = false;   // Below 20% HP buff
//    private Sc_StatEffect _fofTier1ATK, _fofTier1AP, _fofTier2ATK, _fofTier2AP, _fofTier2HST, _fofTier2AS, _fofTier2MS;

//    // Cycle of Life: tracks kill count toward next trigger
//    private int _takedownCount = 0;
//    private int _takedownsPerTrigger = 5;   // Trigger every 5 kills (matches design doc)

//    // Aura drain (Diya's Blessing) coroutine handle
//    private Coroutine _auraDrainCoroutine;

//    // ── Constructor ────────────────────────────────────────────────────────────
//    public Sc_AugmentHandler(Mb_GuardianBase guardian, MonoBehaviour runner)
//    {
//        _guardian = guardian;
//        _runner = runner;

//        // Build the stat dictionary once — maps StatType enum to the actual Sc_Stat object
//        _statDict = new Dictionary<StatType, Sc_Stat>
//        {
//            { StatType.MaxHealth,        guardian.Stats.MaxHealth },
//            { StatType.HealthRegen,      guardian.Stats.HealthRegen },
//            { StatType.MoveSpeed,        guardian.Stats.MoveSpeed },
//            { StatType.AttackSpeed,      guardian.Stats.AttackSpeed },
//            { StatType.AttackPower,      guardian.Stats.AttackPower },
//            { StatType.AbilityPower,     guardian.Stats.AbilityPower },
//            { StatType.CooldownReduction,guardian.Stats.CooldownReduction },
//            { StatType.CriticalChance,   guardian.Stats.CriticalChance },
//            { StatType.CriticalDamage,   guardian.Stats.CriticalDamage },
//            { StatType.Lifesteal,        guardian.Stats.Lifesteal },
//            { StatType.Shielding,        guardian.Stats.Shielding },
//        };
//    }

//    // ── Public API ─────────────────────────────────────────────────────────────

//    /// <summary>
//    /// Call this when the player picks an augment from the UI.
//    /// Returns false and does nothing if already at the max of 3.
//    /// </summary>
//    public bool TryEquipAugment(SO_Augment augment)
//    {
//        if (_equippedAugments.Count >= MAX_AUGMENTS)
//        {
//            Debug.LogWarning($"[AugmentHandler] Cannot equip {augment.AugmentName}: already at max {MAX_AUGMENTS} augments.");
//            return false;
//        }

//        _equippedAugments.Add(augment);
//        ApplyAugment(augment);
//        Debug.Log($"[AugmentHandler] Equipped augment: {augment.AugmentName}");
//        return true;
//    }

//    public List<SO_Augment> GetEquippedAugments() => _equippedAugments;
//    public int AugmentCount => _equippedAugments.Count;

//    // Call this when a wave ends — used by Primal Resonance to reset stacks
//    public void OnWaveEnded()
//    {
//        ResetPrimalResonanceStacks();
//    }

//    // Call this when the player's HP changes — used by Fight or Flight thresholds
//    public void OnHealthChanged(float currentHP, float maxHP)
//    {
//        HandleFightOrFlight(currentHP, maxHP);
//    }

//    // Call this when the player activates any ability — used by Primal Resonance
//    public void OnAbilityActivated()
//    {
//        AddPrimalResonanceStack();
//    }

//    // Call this when a CuBot dies — used by Cycle of Life
//    public void OnEnemyKilled()
//    {
//        HandleCycleOfLife();
//    }

//    // Call this when the player lands a crit — used by Hunter's Instinct
//    public void OnCriticalHit(float damageDealt)
//    {
//        HandleCritHeal(damageDealt);
//    }

//    // Called when the stage is over — cleans up coroutines and event side-effects
//    public void Cleanup()
//    {
//        if (_auraDrainCoroutine != null)
//            _runner.StopCoroutine(_auraDrainCoroutine);
//    }

//    // ── Private: Apply ─────────────────────────────────────────────────────────

//    private void ApplyAugment(SO_Augment augment)
//    {
//        var statEffects = new List<Sc_StatEffect>();

//        foreach (var entry in augment.Effects)
//        {
//            // Behavior-only entries (e.g., AuraDrain) don't have a meaningful stat/value,
//            // so we skip adding them to the Sc_Modifier and instead wire up the behavior.
//            if (entry.Behavior != AugmentBehavior.None)
//            {
//                WireUpBehavior(entry);
//                continue;
//            }

//            // Pure stat entry — convert to Sc_StatEffect and batch it
//            statEffects.Add(new Sc_StatEffect(entry.TargetStat, entry.Value, entry.ModType));
//        }

//        // If there were any pure stat effects, apply them all as one Sc_Modifier
//        if (statEffects.Count > 0)
//        {
//            var modifier = new Sc_Modifier(augment.AugmentName, ModifierSource.Augment, statEffects, float.PositiveInfinity);
//            _appliedModifiers.Add(modifier);
//        }
//    }

//    // Looks at an effect's AugmentBehavior tag and starts the appropriate special logic
//    private void WireUpBehavior(Sc_AugmentEffect entry)
//    {
//        switch (entry.Behavior)
//        {
//            case AugmentBehavior.AuraDrain:
//                // Diya's Blessing: start a repeating drain coroutine
//                _auraDrainCoroutine = _runner.StartCoroutine(AuraDrainRoutine(entry.BehaviorParam1));
//                break;

//            case AugmentBehavior.TakedownCounter:
//                // Cycle of Life: hook into CuBot death event
//                // The actual handling happens in OnEnemyKilled(), which Mb_GuardianBase calls
//                _takedownsPerTrigger = (int)entry.BehaviorParam1;   // e.g., 5
//                Debug.Log($"[AugmentHandler] Cycle of Life active: trigger every {_takedownsPerTrigger} kills.");
//                break;

//            case AugmentBehavior.HPThreshold:
//                // Fight or Flight: store threshold params; checked on OnHealthChanged()
//                Debug.Log($"[AugmentHandler] Fight or Flight active. Thresholds: {entry.BehaviorParam1 * 100}% / {entry.BehaviorParam2 * 100}%");
//                break;

//            case AugmentBehavior.CritHeal:
//                // Hunter's Instinct: no extra setup needed, handled in OnCriticalHit()
//                Debug.Log($"[AugmentHandler] Hunter's Instinct active.");
//                break;

//            case AugmentBehavior.AbilityCastStack:
//                // Primal Resonance: no extra setup needed, handled in OnAbilityActivated()
//                Debug.Log($"[AugmentHandler] Primal Resonance active.");
//                break;

//            case AugmentBehavior.StatConversion:
//                // Natural Selection: one-time calculation at equip time
//                ApplyNaturalSelection();
//                break;
//        }
//    }

//    // ── Reactive Behaviors ─────────────────────────────────────────────────────

//    // Diya's Blessing — every 1 second, heal for 5% of Max HP from nearby enemies.
//    // "Nearby" is defined as within a radius; damage to enemies is a TODO.
//    private IEnumerator AuraDrainRoutine(float drainPercent)
//    {
//        // TODO: Also deal damage to nearby enemies equal to the amount healed
//        float drainRadius = 5f; // TODO: Pull this from design doc / SO field

//        while (true)
//        {
//            yield return new WaitForSeconds(1f);

//            float healAmount = _guardian.Stats.MaxHealth.Value() * drainPercent;
//            _guardian.Health.Heal(healAmount);
//            Debug.Log($"[Diya's Blessing] Healed {healAmount} HP from aura drain.");
//        }
//    }

//    // Cycle of Life — every 5 kills: restore 30% Max HP, permanently gain 5% Max HP
//    private void HandleCycleOfLife()
//    {
//        _takedownCount++;
//        if (_takedownCount % _takedownsPerTrigger != 0) return;

//        // Restore 30% of current Max HP
//        float healAmount = _guardian.Stats.MaxHealth.Value() * 0.30f;
//        _guardian.Health.Heal(healAmount);

//        // Permanently increase Max HP by 5% (flat bonus based on current max)
//        float bonusHP = _guardian.Stats.MaxHealth.Value() * 0.05f;
//        var growthEffect = new Sc_StatEffect(StatType.MaxHealth, bonusHP, StatModType.Flat);
//        _guardian.Stats.MaxHealth.AddEffect(growthEffect, _runner);

//        Debug.Log($"[Cycle of Life] Trigger at {_takedownCount} kills. Healed {healAmount}, Max HP grew by {bonusHP}.");
//    }

//    // Fight or Flight — conditional stat buffs at HP thresholds (40% and 20%)
//    private void HandleFightOrFlight(float currentHP, float maxHP)
//    {
//        float hpRatio = currentHP / maxHP;

//        // Tier 1: below 40% HP
//        if (hpRatio <= 0.40f && !_fofTier1Active)
//        {
//            _fofTier1Active = true;
//            _fofTier1ATK = new Sc_StatEffect(StatType.AttackPower, 50f, StatModType.Flat);
//            _fofTier1AP = new Sc_StatEffect(StatType.AbilityPower, 50f, StatModType.Flat);
//            _guardian.Stats.AttackPower.AddEffect(_fofTier1ATK, _runner);
//            _guardian.Stats.AbilityPower.AddEffect(_fofTier1AP, _runner);
//            Debug.Log("[Fight or Flight] Tier 1 triggered: +50 ATK, +50 AP");
//        }

//        // Tier 2: below 20% HP (stacks on top of Tier 1)
//        if (hpRatio <= 0.20f && !_fofTier2Active)
//        {
//            _fofTier2Active = true;

//            // Additional ATK/AP (total becomes 200 per the design doc range; Tier 1 gave 50, so +150 more)
//            _fofTier2ATK = new Sc_StatEffect(StatType.AttackPower, 150f, StatModType.Flat);
//            _fofTier2AP = new Sc_StatEffect(StatType.AbilityPower, 150f, StatModType.Flat);

//            // TODO: HST stat not yet in Sc_Stat / StatType — using CooldownReduction as placeholder
//            _fofTier2HST = new Sc_StatEffect(StatType.CooldownReduction, 50f, StatModType.Flat);

//            _fofTier2AS = new Sc_StatEffect(StatType.AttackSpeed, 0.50f, StatModType.Percent);
//            _fofTier2MS = new Sc_StatEffect(StatType.MoveSpeed, 0.50f, StatModType.Percent);

//            _guardian.Stats.AttackPower.AddEffect(_fofTier2ATK, _runner);
//            _guardian.Stats.AbilityPower.AddEffect(_fofTier2AP, _runner);
//            _guardian.Stats.CooldownReduction.AddEffect(_fofTier2HST, _runner);
//            _guardian.Stats.AttackSpeed.AddEffect(_fofTier2AS, _runner);
//            _guardian.Stats.MoveSpeed.AddEffect(_fofTier2MS, _runner);
//            Debug.Log("[Fight or Flight] Tier 2 triggered: +150 ATK/AP, +50 HST, +50% AS/MS");
//        }
//    }

//    // Hunter's Instinct — heal 5% of crit damage dealt
//    private void HandleCritHeal(float damageDealt)
//    {
//        bool hasHuntersInstinct = _equippedAugments.Exists(a =>
//            a.Effects.Exists(e => e.Behavior == AugmentBehavior.CritHeal));

//        if (!hasHuntersInstinct) return;

//        float healAmount = damageDealt * 0.05f;
//        _guardian.Health.Heal(healAmount);
//        Debug.Log($"[Hunter's Instinct] Crit heal: {healAmount} HP");
//    }

//    // Primal Resonance — +5% AP and ATK per ability cast this wave, resets on wave end
//    private void AddPrimalResonanceStack()
//    {
//        bool hasPrimalResonance = _equippedAugments.Exists(a =>
//            a.Effects.Exists(e => e.Behavior == AugmentBehavior.AbilityCastStack));

//        if (!hasPrimalResonance) return;

//        _primalResonanceStacks++;
//        float stackValue = 0.05f; // +5% per stack

//        // Each stack is its own Sc_StatEffect so we can remove them individually on reset
//        var atkStack = new Sc_StatEffect(StatType.AttackPower, stackValue, StatModType.Percent);
//        var apStack = new Sc_StatEffect(StatType.AbilityPower, stackValue, StatModType.Percent);

//        _guardian.Stats.AttackPower.AddEffect(atkStack, _runner);
//        _guardian.Stats.AbilityPower.AddEffect(apStack, _runner);

//        _primalStackEffects.Add(atkStack);
//        _primalStackEffects.Add(apStack);

//        Debug.Log($"[Primal Resonance] Stack {_primalResonanceStacks}: +5% ATK/AP");
//    }

//    private void ResetPrimalResonanceStacks()
//    {
//        if (_primalResonanceStacks == 0) return;

//        // Remove every stacked effect we added this wave
//        foreach (var effect in _primalStackEffects)
//        {
//            if (_statDict.TryGetValue(effect.TargetStat, out var stat))
//                stat.RemoveEffect(effect);
//        }

//        _primalStackEffects.Clear();
//        _primalResonanceStacks = 0;
//        Debug.Log("[Primal Resonance] Stacks reset for new wave.");
//    }

//    // Natural Selection — one-time conversion at equip time:
//    // Take whichever of bonus ATK or bonus AP is larger, add 200% of it to the other.
//    private void ApplyNaturalSelection()
//    {
//        float bonusATK = _guardian.Stats.AttackPower.BonusValue();
//        float bonusAP = _guardian.Stats.AbilityPower.BonusValue();

//        if (bonusATK >= bonusAP)
//        {
//            // ATK is greater — convert 200% of bonus ATK into AP
//            float converted = bonusATK * 2.00f;
//            var effect = new Sc_StatEffect(StatType.AbilityPower, converted, StatModType.Flat);
//            _guardian.Stats.AbilityPower.AddEffect(effect, _runner);
//            Debug.Log($"[Natural Selection] Converted {converted} ATK → AP");
//        }
//        else
//        {
//            // AP is greater — convert 200% of bonus AP into ATK
//            float converted = bonusAP * 2.00f;
//            var effect = new Sc_StatEffect(StatType.AttackPower, converted, StatModType.Flat);
//            _guardian.Stats.AttackPower.AddEffect(effect, _runner);
//            Debug.Log($"[Natural Selection] Converted {converted} AP → ATK");
//        }
//    }
//}