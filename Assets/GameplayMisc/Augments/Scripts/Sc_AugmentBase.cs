// Sc_AugmentBase.cs
// The base class for every augment in the game.
//
// HOW AN AUGMENT "STICKS" TO THE PLAYER:
//   1. OnEquip() is called once when the player picks the augment.
//   2. It builds a Sc_Modifier from this augment's SO_Augment data and
//      calls Stats.AddModifier() on the player — that modifier now lives
//      inside Mb_StatBlock for the rest of the stage.
//   3. The augment instance itself is stored in Mb_AugmentManager's list,
//      which keeps it alive and reachable for the entire stage.
//   4. OnUnequip() hands the exact same modifier back to RemoveModifier(),
//      surgically removing only this augment's effects — other augments are untouched.
//
// DERIVED CLASSES:
//   - Simple augments (Wings of Balance, Feral Surge, etc.): override nothing.
//     The base class handles everything from the SO's Effects list.
//   - Conditional augments (Fight or Flight, Primal Resonance, etc.):
//     override OnEquip, OnUnequip, and/or OnWaveEnd to wire up their special logic.

using System.Collections.Generic;

public abstract class Sc_AugmentBase
{
    // The ScriptableObject that holds this augment's name, icon, description, and stat effects
    protected SO_Augment _Data;

    // The character this augment is attached to — needed to reach Stats and Health
    protected Mb_CharacterBase _Owner;

    // The modifier we created and gave to StatBlock.
    // We hold onto this reference so we can remove exactly this modifier later —
    // not all augment modifiers, just ours.
    private Sc_Modifier _appliedModifier;

    // Constructor — every derived class calls this via base(data, owner)
    protected Sc_AugmentBase(SO_Augment data, Mb_CharacterBase owner)
    {
        _Data = data;
        _Owner = owner;
    }


    // -------------------------------------------------------------------------
    // Core Lifecycle — called by Mb_AugmentManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called once when the player picks this augment from the rewards panel.
    /// Applies all stat effects from the SO to the player's StatBlock.
    /// Derived classes that override this MUST call base.OnEquip(owner)
    /// if they still want the SO's stat effects applied.
    /// </summary>
    public virtual void OnEquip(Mb_CharacterBase owner)
    {
        // If the SO has no effects (conditional augment), skip modifier creation.
        // The derived class handles everything itself.
        if (_Data.Effects == null || _Data.Effects.Count == 0) return;

        // Wrap all the SO's effects into one named modifier bundle.
        // ModifierSource.Augment lets us identify these for cleanup if needed.
        _appliedModifier = new Sc_Modifier(
            _Data.AugmentName,
            ModifierSource.Augment,
            new List<Sc_StatEffect>(_Data.Effects) // copy so we own the list
        );

        owner.Stats.AddModifier(_appliedModifier);
    }


    /// <summary>
    /// Called at the end of the stage (or on a full reset).
    /// Removes exactly this augment's modifier from StatBlock — no other augments are affected.
    /// Derived classes that override this MUST call base.OnUnequip(owner) to clean up stat effects.
    /// </summary>
    public virtual void OnUnequip(Mb_CharacterBase owner)
    {
        // Only remove if we actually applied a modifier earlier
        if (_appliedModifier == null) return;

        owner.Stats.RemoveModifier(_appliedModifier);
        _appliedModifier = null;
    }


    /// <summary>
    /// Called by Mb_AugmentManager at the end of every wave.
    /// Most augments do nothing here — override in derived classes that need
    /// per-wave resets (e.g. Primal Resonance resets its stacks each wave).
    /// </summary>
    public virtual void OnWaveEnd() { }


    // -------------------------------------------------------------------------
    // Helpers for derived classes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Convenience method for conditional augments that need to apply
    /// a modifier they built themselves (not from the SO's Effects list).
    /// Stores it so OnUnequip can remove it cleanly.
    /// </summary>
    protected void ApplyCustomModifier(Sc_Modifier modifier)
    {
        _appliedModifier = modifier;
        _Owner.Stats.AddModifier(modifier);
    }


    /// <summary>
    /// Removes the currently applied modifier without fully unequipping the augment.
    /// Useful for augments that need to swap modifiers mid-stage
    /// (e.g. Fight or Flight updating its tier as HP changes).
    /// </summary>
    protected void RemoveCurrentModifier()
    {
        if (_appliedModifier == null) return;
        _Owner.Stats.RemoveModifier(_appliedModifier);
        _appliedModifier = null;
    }


    // Read-only access so UI and manager can display augment identity
    public string AugmentName => _Data.AugmentName;
    public string Description => _Data.Description;
    public UnityEngine.Sprite Icon => _Data.Icon;
}