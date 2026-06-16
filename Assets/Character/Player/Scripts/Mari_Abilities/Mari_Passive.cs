// Mari_Passive.cs
// [Passive] Mental Overflow — Mari's passive ability.
//
// STACK BEHAVIOR:
//   - Each CuBot takedown grants 1 stack (max 5).
//   - Each stack permanently increases Ability Power by AP_PER_STACK (flat modifier).
//   - The modifier is rebuilt (removed + reapplied) on every stack change —
//     StatBlock never holds two Mental Overflow modifiers simultaneously.
//
// OVERCHARGE:
//   - Reaching MAX_STACKS sets IsOvercharged = true.
//   - Mari_Q and Mari_E read IsOvercharged before activating.
//   - After an overcharged Q or E fires, they call ConsumeOvercharge() here,
//     which resets all stacks to 0, removes the AP modifier, and clears the flag.
//
// VISUAL:
//   - _StackVFX: a looping particle system on Mari herself (e.g. psychic sparks)
//     that updates intensity/count based on current stacks. Assigned in Inspector.
//   - _OverchargeVFX: a separate particle system that plays when stacks hit 5
//     (e.g. bright pulsing aura). Stopped on ConsumeOvercharge().
//   - Both are optional — null-checked so missing assets don't crash.
//
// HOW Q AND E CONSUME OVERCHARGE:
//   1. At activation, cast: (Mari_Passive)_User.Abilities.GetAbilityBySlot(AbilitySlot.Passive)
//   2. Check passive.IsOvercharged
//   3. Apply doubled effects
//   4. Call passive.ConsumeOvercharge()

using System.Collections.Generic;
using UnityEngine;

public class Mari_Passive : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const int MAX_STACKS = 5;

    // Flat AP bonus per stack — TODO: expose in SO_Ability scaling table when tuning
    private const float AP_PER_STACK = 10f;


    // -------------------------------------------------------------------------
    // Inspector-Assigned VFX
    // These are fetched from Mari's GameObject in OnEquip via GetComponentsInChildren
    // OR assigned directly. We store them as fields set during OnEquip.
    // -------------------------------------------------------------------------

    // Looping particle system that represents current stacks (e.g. orbiting sparks).
    // Play/stop and emission rate updated as stacks change.
    // TODO: Assign Particle System named "StackVFX" as a child of Mari's prefab.
    private ParticleSystem _stackVFX;

    // Separate particle system that fires when overcharge is reached.
    // TODO: Assign Particle System named "OverchargeVFX" as a child of Mari's prefab.
    private ParticleSystem _overchargeVFX;


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    // Current stack count — 0 to MAX_STACKS
    private int _currentStacks = 0;

    // The single active AP modifier — rebuilt on every stack change
    private Sc_Modifier _stackModifier = null;

    // Read by Mari_Q and Mari_E to decide whether to apply overcharged effects
    public bool IsOvercharged { get; private set; } = false;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Mari_Passive(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        MB_CuBotBase.OnCuBotDeath += HandleTakedown;

        // Locate VFX particle systems by name in Mari's prefab hierarchy.
        // Named lookup keeps this decoupled from Inspector slot assignments —
        // Leo just needs to name the particle system GameObjects correctly.
        foreach (ParticleSystem ps in user.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "StackVFX") _stackVFX = ps;
            if (ps.gameObject.name == "OverchargeVFX") _overchargeVFX = ps;
        }

        // Ensure both VFX start stopped on equip
        _stackVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _overchargeVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Debug.Log("[Mental Overflow] Equipped.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        MB_CuBotBase.OnCuBotDeath -= HandleTakedown;

        RemoveStackModifier();

        _stackVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _overchargeVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _currentStacks = 0;
        IsOvercharged = false;

        Debug.Log("[Mental Overflow] Unequipped.");
    }


    // -------------------------------------------------------------------------
    // Takedown Handler
    // -------------------------------------------------------------------------

    private void HandleTakedown(GameObject killedCuBot)
    {
        // Already at max — no point adding more
        if (_currentStacks >= MAX_STACKS) return;

        _currentStacks++;
        RebuildStackModifier();
        UpdateStackVFX();

        Debug.Log($"[Mental Overflow] Stack gained — {_currentStacks}/{MAX_STACKS}.");

        if (_currentStacks >= MAX_STACKS)
            TriggerOvercharge();
    }


    // -------------------------------------------------------------------------
    // Overcharge
    // -------------------------------------------------------------------------

    private void TriggerOvercharge()
    {
        IsOvercharged = true;

        // Stop the regular stack VFX and play the overcharge burst
        _stackVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (_overchargeVFX != null)
        {
            _overchargeVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _overchargeVFX.Play();
        }

        Debug.Log("[Mental Overflow] OVERCHARGED — next Q or E is empowered.");
    }


    /// <summary>
    /// Called by Mari_Q or Mari_E immediately after applying overcharged effects.
    /// Resets all stacks, removes the AP modifier, clears the overcharge flag,
    /// and stops the overcharge VFX.
    /// </summary>
    public void ConsumeOvercharge()
    {
        IsOvercharged = false;
        _currentStacks = 0;

        RemoveStackModifier();
        UpdateStackVFX();

        _overchargeVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Debug.Log("[Mental Overflow] Overcharge consumed. All stacks reset.");
    }


    // -------------------------------------------------------------------------
    // Modifier Management
    // -------------------------------------------------------------------------

    // Removes the old modifier and applies a fresh one for the current stack count.
    // If stacks are 0, only removes — no new modifier is applied.
    private void RebuildStackModifier()
    {
        RemoveStackModifier();

        if (_currentStacks <= 0) return;

        _stackModifier = BuildModifier(
            "Mental Overflow AP Bonus",
            ModifierSource.Ability,
            new Sc_StatEffect(
                StatType.AbilityPower,
                AP_PER_STACK * _currentStacks,
                StatModType.Flat
            )
        );

        ApplyToSelf(_User, _stackModifier);
    }


    private void RemoveStackModifier()
    {
        if (_stackModifier == null) return;
        _User.Stats.RemoveModifier(_stackModifier);
        _stackModifier = null;
    }


    // -------------------------------------------------------------------------
    // VFX Helpers
    // -------------------------------------------------------------------------

    // Updates the stack particle system emission rate based on current stack count.
    // 0 stacks → stopped. 1–4 stacks → playing with scaled emission rate.
    // Overcharge VFX takes over at 5 stacks — stack VFX is stopped there.
    private void UpdateStackVFX()
    {
        if (_stackVFX == null) return;

        if (_currentStacks <= 0)
        {
            _stackVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        // Scale emission rate linearly: 1 stack = 20%, 4 stacks = 80% of max rate.
        // TODO: Tune base emission rate on the particle system asset itself (Leo).
        // The multiplier here is a 0–1 normalized intensity, not a raw particle count.
        var emission = _stackVFX.emission;
        float intensity = (float)_currentStacks / MAX_STACKS;

        // rateOverTime is a MinMaxCurve — we set the constant value directly
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(
            Mathf.Lerp(5f, 25f, intensity) // TODO: tune min/max emission counts
        );

        if (!_stackVFX.isPlaying)
            _stackVFX.Play();
    }


    // -------------------------------------------------------------------------
    // Public Read API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the current stack count. Read by UI to display the stack counter.
    /// </summary>
    public int GetCurrentStacks() => _currentStacks;

    /// <summary>
    /// Returns the maximum stack count. Read by UI.
    /// </summary>
    public int GetMaxStacks() => MAX_STACKS;
}