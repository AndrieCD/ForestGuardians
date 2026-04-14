// Passive_Ability.cs
// [Passive] Royal Plumage — Rajah Bagwis's passive ability.
//
// STACK TIMING BEHAVIOR:
//   - Each basic attack hit adds 1 stack (max 10) and resets a shared 2s inactivity timer.
//   - If no hit lands within 2 seconds, stacks begin falling off one by one every 0.5s.
//   - Landing a hit during falloff immediately stops the decay, adds a stack, and
//     restarts the 2s inactivity timer — stacks are saved at their current count.
//
// TIER SYSTEM (cumulative — higher tiers include all lower bonuses):
//   Tier 0 (0–24 takedowns)  : +AS per stack
//   Tier 1 (25–49 takedowns) : +AS, +ATK per stack
//   Tier 2 (50–99 takedowns) : +AS, +ATK, +CritChance per stack
//   Tier 3 (100+ takedowns)  : All bonuses are 50% more effective
//
// MODIFIER PATTERN:
//   One Sc_Modifier is active at a time. It is removed and rebuilt whenever
//   stacks or tier changes — StatBlock never holds two Royal Plumage modifiers.
//
// STATIC EVENT:
//   OnBasicAttackHit is static so Primary and Secondary can fire it without
//   holding a reference to the passive. The passive subscribes in OnEquip
//   and unsubscribes in OnUnequip.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Passive_Ability : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Static Event — fired by Primary and Secondary on each successful hit
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fired by Rajah_Primary and Rajah_Secondary after a successful basic attack.
    /// The passive subscribes to this without the attack abilities knowing anything
    /// about the passive directly.
    /// </summary>
    public static event Action OnBasicAttackHit;


    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const int MAX_STACKS = 10;
    private const float INACTIVITY_WINDOW = 2f;    // Seconds before decay begins
    private const float DECAY_INTERVAL = 0.5f;  // Seconds between each stack falling off

    private const int TIER_1_THRESHOLD = 25;
    private const int TIER_2_THRESHOLD = 50;
    private const int TIER_3_THRESHOLD = 100;

    // Base bonus per stack — TODO: move to SO_Ability scaling table when tuning frequently
    private const float AS_PER_STACK = 0.04f; // +4% AttackSpeed per stack (Percent)
    private const float ATK_PER_STACK = 5f;    // +5 flat AttackPower per stack (Flat)
    private const float CRIT_CHANCE_PER_STACK = 1f;    // +1% CritChance per stack (Flat)
    private const float TIER_3_MULTIPLIER = 1.5f;  // Tier 3 makes all bonuses 50% stronger


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    private int _currentStacks = 0;
    private int _takedownCount = 0;
    private int _currentTier = 0;

    // The single active modifier representing current stack bonuses
    // Replaced entirely whenever stacks or tier changes
    private Sc_Modifier _stackModifier = null;

    // Tracks which decay state we are in so HandleBasicAttackHit knows what to cancel
    private enum StackState { Idle, Active, Decaying }
    private StackState _stackState = StackState.Idle;

    // Only one of these runs at a time — never both simultaneously
    private Coroutine _inactivityCoroutine = null;
    private Coroutine _decayCoroutine = null;

    // Stored at OnEquip so coroutines can be started/stopped without a MonoBehaviour cast
    private MonoBehaviour _runner = null;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Passive_Ability(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        _runner = user;

        OnBasicAttackHit += HandleBasicAttackHit;
        MB_CuBotBase.OnCuBotDeath += HandleTakedown;

        Debug.Log($"[{user.name}] Royal Plumage equipped.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        OnBasicAttackHit -= HandleBasicAttackHit;
        MB_CuBotBase.OnCuBotDeath -= HandleTakedown;

        StopAllStackCoroutines();
        RemoveStackModifier();

        // Reset everything cleanly in case this character is ever reused
        _currentStacks = 0;
        _takedownCount = 0;
        _currentTier = 0;
        _stackState = StackState.Idle;

        Debug.Log($"[{user.name}] Royal Plumage unequipped.");
    }


    // -------------------------------------------------------------------------
    // Basic Attack Handler
    // -------------------------------------------------------------------------

    private void HandleBasicAttackHit()
    {
        // Stop whatever is currently running — inactivity timer or falloff loop
        // Landing a hit always interrupts both and takes full control
        StopAllStackCoroutines();

        // Add one stack, clamped at the maximum
        _currentStacks = Mathf.Min(_currentStacks + 1, MAX_STACKS);

        // Rebuild the modifier to reflect the new stack count
        RebuildStackModifier();

        // Start the inactivity window — if no hit lands in 2s, decay begins
        _stackState = StackState.Active;
        _inactivityCoroutine = _runner.StartCoroutine(InactivityRoutine());

        Debug.Log($"[Royal Plumage] Hit — Stacks: {_currentStacks}/{MAX_STACKS} | " +
                  $"Tier: {_currentTier} | State: {_stackState}");
    }
    // Call this method to raise the OnBasicAttackHit event
    public static void RaiseBasicAttackHit()
    {
        OnBasicAttackHit?.Invoke();
    }


    // -------------------------------------------------------------------------
    // Takedown Handler
    // -------------------------------------------------------------------------

    private void HandleTakedown()
    {
        _takedownCount++;

        int newTier = CalculateTier(_takedownCount);

        // Only rebuild if the tier changed — avoids unnecessary StatBlock churn
        if (newTier != _currentTier)
        {
            _currentTier = newTier;
            RebuildStackModifier();
            Debug.Log($"[Royal Plumage] Tier {_currentTier} unlocked at {_takedownCount} takedowns.");
        }
    }


    // -------------------------------------------------------------------------
    // Coroutines
    // -------------------------------------------------------------------------

    // Waits INACTIVITY_WINDOW seconds after the last hit.
    // If no hit interrupts it, transitions to the decay phase.
    private IEnumerator InactivityRoutine()
    {
        yield return new WaitForSeconds(INACTIVITY_WINDOW);

        // 2 seconds passed with no hit — begin falling off stacks one by one
        _inactivityCoroutine = null;
        _stackState = StackState.Decaying;
        _decayCoroutine = _runner.StartCoroutine(DecayRoutine());
    }


    // Removes one stack every DECAY_INTERVAL seconds until all are gone.
    // Can be interrupted at any point by HandleBasicAttackHit (a new hit).
    private IEnumerator DecayRoutine()
    {
        while (_currentStacks > 0)
        {
            yield return new WaitForSeconds(DECAY_INTERVAL);

            // Remove one stack and update the modifier
            _currentStacks--;
            RebuildStackModifier();

            Debug.Log($"[Royal Plumage] Stack fell off — Stacks remaining: {_currentStacks}");
        }

        // All stacks gone — passive is fully idle
        _decayCoroutine = null;
        _stackState = StackState.Idle;

        Debug.Log("[Royal Plumage] All stacks expired.");
    }


    // Stops whichever coroutine is currently running.
    // Always call this before starting a new coroutine to guarantee only one runs at a time.
    private void StopAllStackCoroutines()
    {
        if (_inactivityCoroutine != null)
        {
            _runner.StopCoroutine(_inactivityCoroutine);
            _inactivityCoroutine = null;
        }

        if (_decayCoroutine != null)
        {
            _runner.StopCoroutine(_decayCoroutine);
            _decayCoroutine = null;
        }
    }


    // -------------------------------------------------------------------------
    // Tier Calculation
    // -------------------------------------------------------------------------

    private int CalculateTier(int takedowns)
    {
        if (takedowns >= TIER_3_THRESHOLD) return 3;
        if (takedowns >= TIER_2_THRESHOLD) return 2;
        if (takedowns >= TIER_1_THRESHOLD) return 1;
        return 0;
    }


    // -------------------------------------------------------------------------
    // Modifier Management
    // -------------------------------------------------------------------------

    // Removes the old modifier and applies a freshly built one.
    // Called on every stack count or tier change — StatBlock never holds
    // more than one Royal Plumage modifier at a time.
    private void RebuildStackModifier()
    {
        RemoveStackModifier();

        if (_currentStacks <= 0) return;

        _stackModifier = BuildStackModifier(_currentStacks, _currentTier);
        ApplyToSelf(_User, _stackModifier);
    }


    private void RemoveStackModifier()
    {
        if (_stackModifier == null) return;
        _User.Stats.RemoveModifier(_stackModifier);
        _stackModifier = null;
    }


    // Constructs the modifier for the current stack count and tier.
    // All effects are calculated fresh — this is intentional since stacks
    // change frequently and we always want an accurate snapshot.
    private Sc_Modifier BuildStackModifier(int stacks, int tier)
    {
        // Tier 3 amplifies all bonuses by 50%
        float m = (tier >= 3) ? TIER_3_MULTIPLIER : 1f;

        List<Sc_StatEffect> effects = new List<Sc_StatEffect>();

        // All tiers: AttackSpeed bonus (Percent — 0.04 = 4% per stack)
        effects.Add(new Sc_StatEffect(
            StatType.AttackSpeed,
            AS_PER_STACK * stacks * m,
            StatModType.Percent
        ));

        // Tier 1+: AttackPower bonus (Flat)
        if (tier >= 1)
            effects.Add(new Sc_StatEffect(
                StatType.AttackPower,
                ATK_PER_STACK * stacks * m,
                StatModType.Flat
            ));

        // Tier 2+: CriticalChance bonus (Flat — 1% per stack)
        if (tier >= 2)
            effects.Add(new Sc_StatEffect(
                StatType.CriticalChance,
                CRIT_CHANCE_PER_STACK * stacks * m,
                StatModType.Flat
            ));

        // Duration is omitted (permanent) because the coroutines handle removal,
        // not StatBlock's timed removal — this gives us full control over decay timing
        return new Sc_Modifier(
            "Royal Plumage Stacks",
            ModifierSource.Ability,
            effects
        );
    }
}