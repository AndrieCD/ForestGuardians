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
    private static long _nextRuntimeId;
    private readonly List<Sc_StatEffect> _effects;

    /// <summary>
    /// Runtime identity used to distinguish otherwise identical modifier instances.
    /// </summary>
    public long RuntimeId { get; }

    public IReadOnlyList<Sc_StatEffect> Effects => _effects;
    public readonly float Duration; // float.PositiveInfinity = permanent

    public Sc_Modifier(string name, ModifierSource source, List<Sc_StatEffect> effects, float duration = float.PositiveInfinity)
    {
        RuntimeId = ++_nextRuntimeId;
        ModifierName = string.IsNullOrWhiteSpace(name) ? "Unnamed Modifier" : name;
        Source = source;
        _effects = new List<Sc_StatEffect>();
        if (effects != null)
        {
            foreach (Sc_StatEffect effect in effects)
                _effects.Add(effect?.Clone());
        }
        Duration = duration;
    }

    public override string ToString()
    {
        return $"{ModifierName} [{Source}] (RuntimeId: {RuntimeId})";
    }
}
