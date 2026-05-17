// E_StatusType.cs
// Defines all status effect types that can be applied to characters in Forest Guardians.
// Used by Mb_StatusEffectController to track active effects without string keys.
//
// HOW TO ADD A NEW STATUS:
//   1. Add a value here with a comment explaining its gameplay meaning.
//   2. Add a static factory method in Sc_StatusEffect for easy construction.
//   3. Wire the application call into the relevant ability script's Activate().

public enum StatusType
{
    // Reduces the target's Move Speed for a duration.
    // Applied by abilities that impede enemy pathing (e.g. Mindspikes).
    MoveSlow,

    // Reduces the target's Attack Speed for a duration.
    AttackSlow,

    // Completely prevents the target from moving or using abilities.
    // Implemented via ActionDisableFlags.Stun on Guardians;
    // stops NavMeshAgent on CuBots.
    // TODO: CuBot stun requires Mb_CuBotController to subscribe to OnStatusApplied
    //       and call _Agent.isStopped = true while Stun is active.
    Stun,

    // Prevents movement but allows attacks and abilities.
    // Applied via ActionDisableFlags.Root on Guardians.
    // TODO: Not yet wired into any ability — placeholder for future design.
    Root,

    // Prevents ability use but allows movement and basic attacks.
    // Applied via ActionDisableFlags.Silence on Guardians.
    // TODO: Not yet wired into any ability — placeholder for future design.
    Silence,

    // Deals damage over time every TickInterval seconds.
    // No stat modifier — pure DoT.
    // Applied by abilities involving toxins or decay (e.g. Toxion CuBot).
    Poison,

    // Deals damage over time every TickInterval seconds, with a higher tick rate than Poison.
    // No stat modifier — pure DoT.
    // Applied by Bernie and fire-themed abilities.
    Burn,
}