// SO_WaveData.cs (Updated)
using System.Collections.Generic;
using UnityEngine;

public enum EliteAuraType
{
    Random,
    Commander,
    Guardian
}

[CreateAssetMenu(fileName = "NewWaveData_SO", menuName = "Stage/Wave_SO")]
public class SO_WaveData : ScriptableObject
{
    //public int MAX_ENEMY_COUNT = 10;

    // Contains the enemy type and count for each wave. This list can be expanded to include more waves as needed.
    [Header("Enemy Data")]
    public List<EnemyWaveEntry> enemyDataList = new List<EnemyWaveEntry>( );


    [Header("Reward")]
    [Tooltip("The reward offered to the player after this wave clears. None = no panel shown.")]
    public RewardType WaveReward = RewardType.None;

    [Header("Elite CuBot")]
    [Tooltip("If true, one eligible CuBot in this wave can spawn as an Elite.")]
    public bool CanSpawnElite = false;

    [Tooltip("Aura used by the Elite. Random chooses Commander or Guardian when the Elite is spawned.")]
    public EliteAuraType EliteAura = EliteAuraType.Random;

}
