using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rajah_Q_Ability : Sc_BaseAbility
{
    public Rajah_Q_Ability(SO_Ability abilityObject, Mb_GuardianBase user) : base(abilityObject, user)
    {
    }


    // 1. Called when the Character spawns (Setup Passive)
    public override void OnEquip(Mb_GuardianBase user)
    {
        // Debug
        Debug.Log($"{user.name} has equipped {this._AbilityData.AbilityName}.");
    }

    // 2. Called when the Player presses the button (Active)
    public override void Activate(Mb_GuardianBase user)
    {
        // Debug
        Debug.Log($"{user.name} has activated {this._AbilityData.AbilityName}.");


        ////// EXAMPLE EFFECT ///
        // Example: +10% MoveSpeed, -20 Flat HP
        List<Sc_StatEffect> effects = new List<Sc_StatEffect>
        {
            new Sc_StatEffect(StatType.MoveSpeed, 0.10f, StatModType.Percent, 2f),
            new Sc_StatEffect(StatType.MaxHealth, -20f, StatModType.Flat)
        };  // The list or bundled effects to apply

        Dictionary<StatType, Sc_Stat> playerStatsDict = new Dictionary<StatType, Sc_Stat>
        {
            { StatType.MoveSpeed, user.MoveSpeed },
            { StatType.MaxHealth, user.MaxHealth }
        }; // A dictionary to map StatTypes to the player's actual stats

        // Constructing the modifier with name "Sample" lasting 10 seconds
        // The modifier itself lasts for 10 seconds, but individual effects can have their own durations
        Sc_Modifier modifier = new Sc_Modifier("Sample", effects, playerStatsDict, user, 10f);

    }

    // 3. Called when Character dies or ability is swapped (Cleanup)
    public override void OnUnequip(Mb_GuardianBase user)
    {
        Debug.Log($"{user.name} has unequipped {this._AbilityData.AbilityName}.");
    }
}
