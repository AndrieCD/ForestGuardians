// Sc_BranchOption.cs
// Represents one ultimate branch choice — bundles everything the rewards system
// needs for one branch into a single object.
//
// WHY A DELEGATE INSTEAD OF A FACTORY CLASS:
//   Each guardian defines their own CreateAbility function inline when they override
//   DefineBranches(). This means adding a new guardian never requires touching any
//   shared system — the guardian is its own factory for its own abilities.
//
// USAGE:
//   1. Guardian overrides DefineBranches() and returns two Sc_BranchOption instances.
//   2. Mb_RewardsManager calls GetBranchOptions() to get them.
//   3. UI reads DisplayData for the card (name, description, icon).
//   4. On player choice, RewardsManager calls CreateAbility(owner) to get the instance.

using System;

public class Sc_BranchOption
{
    // Identity data for the UI card — name, description, icon
    public SO_UltimateBranch DisplayData;

    // The ability SO for this branch — holds cooldown and per-level scaling.
    // Stored here so derived guardian classes can reference it when building
    // the CreateAbility delegate below.
    public SO_Ability AbilityData;

    // When called with the owning character, returns a ready-to-use ability instance.
    // Defined inline by each guardian in DefineBranches() — no central factory needed.
    // Example: owner => new Rajah_R_Branch1(abilityData, owner)
    public Func<Mb_CharacterBase, Sc_BaseAbility> CreateAbility;
}