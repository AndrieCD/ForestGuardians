using System.Collections.Generic;
using UnityEngine;

public class Sc_HeartOfTheForest: Sc_AugmentBase
{
    public Sc_HeartOfTheForest() : base( )
    {
        _AugmentName = "Heart of the Forest";
        
        // Effects list
        _StatEffects.Add(new Sc_StatEffect(StatType.HealthRegen, 0.2f, StatModType.Percent));
        _StatEffects.Add(new Sc_StatEffect(StatType.MaxHealth, 1000f, StatModType.Flat));
    }

    public override void ApplyAugment()
    {
        var playerController = GameObject.FindGameObjectWithTag("Player").GetComponent<Mb_PlayerController>( );
        if (playerController is Mb_GuardianBase targetUser)
        {
            var playerStatsDict = new Dictionary<StatType, Sc_Stat>
            {
                { StatType.HealthRegen, targetUser.HealthRegen},
                { StatType.MaxHealth, targetUser.MaxHealth }
            };

            var modifier = new Sc_Modifier(_AugmentName, _StatEffects, playerStatsDict, targetUser, float.PositiveInfinity);
        }
    }
}
