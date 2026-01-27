using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sc_Modifier
{
    // A Modifier is a collection of StatEffects applied to a target's stats
    // It can contain multiple effects such as buffs or debuffs
    public readonly string ModifierName;
    public List<Sc_StatEffect> Effects = new List<Sc_StatEffect>( );
    public MonoBehaviour TargetBaseScript;      // The base script of the target (eg. Mb_GuardianBase)
    public readonly float Duration;             // float.PositiveInfinity for permanent modifiers, 9999 "wave permanent"

    // Reference to the target's stat dictionary or stat manager
    private Dictionary<StatType, Sc_Stat> _targetStats;

    public Sc_Modifier(string name, List<Sc_StatEffect> effects, Dictionary<StatType, Sc_Stat> targetStats, MonoBehaviour targetBaseScript, float duration = float.PositiveInfinity)
    {
        ModifierName = name;
        Effects = effects;
        _targetStats = targetStats;
        TargetBaseScript = targetBaseScript;
        Duration = duration;
        Apply( );

        // Add the modifier to the target object's modifier list for tracking and removal after each wave
        // Get the interface I_StatModifiable from the targetBaseScript
        if (targetBaseScript is I_StatModifiable statModifiable)
            statModifiable.AddModifier(this);
    }

    public void Apply( )
    {
        foreach (var effect in Effects)
        {
            if (_targetStats.TryGetValue(effect.TargetStat, out var stat))  // Get the target stat from the dictionary, creating a variable 'stat' to hold it
            {
                stat.AddEffect(effect, TargetBaseScript);
            }
        }
        if (Duration == float.PositiveInfinity) return;
        TargetBaseScript.StartCoroutine(RemoveAfterDuration());
    }

    IEnumerator RemoveAfterDuration()
    {
        yield return new WaitForSeconds(Duration);
        Remove();
    }

    public void Remove( )
    {
        foreach (var effect in Effects)
        {
            if (_targetStats.TryGetValue(effect.TargetStat, out var stat))
            {
                stat.RemoveEffect(effect);
            }
        }
    }
}
