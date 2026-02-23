// SO_WaveData.cs (Updated)
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewWaveData_SO", menuName = "Stage/Wave_SO")]
public class SO_WaveData : ScriptableObject
{
    //public int MAX_ENEMY_COUNT = 10;

    // Contains the enemy type and count for each wave. This list can be expanded to include more waves as needed.
    public List<EnemyWaveEntry> enemyDataList = new List<EnemyWaveEntry>( );





}