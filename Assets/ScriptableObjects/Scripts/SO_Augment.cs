// SO_Augment.cs
// ScriptableObject that holds the identity data for one augment:
// its name, description, icon, and the list of stat effects it applies.
//
// Simple augments (e.g. Wings of Balance, Feral Surge) populate the Effects list
// directly — the base class reads them and applies them automatically.
//
// Conditional augments (e.g. Fight or Flight, Primal Resonance) leave Effects empty
// and handle everything in their derived Sc_AugmentBase subclass instead.
//
// Create one SO per augment in the Project window:
//   Right-click > Create > ForestGuardians > Augment

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Augment", menuName = "Augment")]
public class SO_Augment : ScriptableObject
{
    [Header("Identity")]
    public string AugmentName;
    [TextArea] public string Description;
    public Sprite Icon;

    [Header("Stat Effects")]
    // For simple augments: fill this list in the Inspector.
    // Each entry is one stat change (e.g. +60% AS, +40% MS).
    // For conditional augments: leave this empty — logic is in the derived class.
    public List<Sc_StatEffect> Effects = new List<Sc_StatEffect>();
}