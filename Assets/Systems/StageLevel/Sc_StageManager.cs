using System;
using UnityEngine;

// This class is responsible for managing the overall current stage, including controlling the flow of waves via WaveManager,
// tracking player stage progress, handling player and enemy stat scaling, and such.
public class Mb_StageManager : MonoBehaviour
{
    [SerializeField] SO_StageData _CurrentStageData; // This should be assigned in the Inspector with the appropriate StageData SO.

    #region EVENTS
    public static event Action OnStageStart;
    public static event Action OnStageEnd;
    #endregion

    private void Awake( )
    {
        Cursor.visible = false;
    }

    private void Start( )
    {
        StartStage( );
    }


    public SO_StageData GetStageData( )
    {
        return _CurrentStageData;
    }


    private void StartStage()
    {
        Debug.Log($"Start Stage");

        GameManager.Instance.ChangeState(GameState.Playing);
        OnStageStart?.Invoke( );
    }


    public void EndStage()
    {
        OnStageEnd?.Invoke( );
    }

}
