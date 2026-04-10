//using System.Collections.Generic;
//using UnityEngine;

//public class Sc_FeralSurge: Sc_AugmentBase
//{
//    public Sc_FeralSurge() : base( )
//    {
//        _AugmentName = "Feral Surge";
        
//        // Effects list
//        _StatEffects.Add(new Sc_StatEffect(StatType.AttackSpeed, 0.2f, StatModType.Percent));
//        _StatEffects.Add(new Sc_StatEffect(StatType.MoveSpeed, 0.2f, StatModType.Percent));
//    }

//    public override void ApplyAugment()
//    {
//        var playerController = GameObject.FindGameObjectWithTag("Player").GetComponent<Mb_PlayerController>( );
//        if (playerController is Mb_GuardianBase targetUser)
//        {
//            var playerStatsDict = new Dictionary<StatType, Sc_Stat>
//            {
//                { StatType.AttackSpeed, targetUser.Stats.AttackSpeed },
//                { StatType.MoveSpeed, targetUser.Stats.MoveSpeed }
//            };

//            var modifier = new Sc_Modifier(_AugmentName, ModifierSource.Augment, _StatEffects, float.PositiveInfinity);
//            targetUser.Stats.AddModifier(modifier);
//        }
//    }
//}
