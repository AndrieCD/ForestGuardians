using System.Collections.Generic;
using UnityEngine;

public class Sc_WingsOfBalance: Sc_AugmentBase
{
    public Sc_WingsOfBalance() : base( )
    {
        _AugmentName = "Wings of Balance";
        
        // Effects list
        _StatEffects.Add(new Sc_StatEffect(StatType.AttackSpeed, 0.2f, StatModType.Percent));
        _StatEffects.Add(new Sc_StatEffect(StatType.MoveSpeed, 0.2f, StatModType.Percent));
    }

    public override void ApplyAugment()
    {
        var playerController = GameObject.FindGameObjectWithTag("Player").GetComponent<Mb_PlayerController>( );
        if (playerController is Mb_GuardianBase targetUser)
        {
            var playerStatsDict = new Dictionary<StatType, Sc_Stat>
            {
                { StatType.AttackSpeed, targetUser.AttackSpeed },
                { StatType.MoveSpeed, targetUser.MoveSpeed }
            };

            var modifier = new Sc_Modifier(_AugmentName, _StatEffects, playerStatsDict, targetUser, float.PositiveInfinity);
        }
    }
}
