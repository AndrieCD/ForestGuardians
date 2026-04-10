using System.Collections.Generic;

/// <summary>
/// A named bundle of stat effects applied together as one unit.
/// Tagged with a ModifierSource so it can be selectively removed
/// (e.g. "remove all Augment modifiers" or "remove all Environmental debuffs").
///
/// Modifiers don't run coroutines themselves — timed removal is handled
/// by Mb_StatBlock, which has a reliable MonoBehaviour lifetime.
/// </summary>
public class Sc_Modifier
{
    public readonly string ModifierName;
    public readonly ModifierSource Source;
    public readonly List<Sc_StatEffect> Effects;
    public readonly float Duration; // float.PositiveInfinity = permanent

    public Sc_Modifier(string name, ModifierSource source, List<Sc_StatEffect> effects, float duration = float.PositiveInfinity)
    {
        ModifierName = name;
        Source = source;
        Effects = effects;
        Duration = duration;
    }
}