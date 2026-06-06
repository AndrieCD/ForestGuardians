using System;
using System.Collections;
using UnityEngine;

public class Mb_StageManager : MonoBehaviour
{
    [SerializeField] SO_StageData _CurrentStageData;

    #region EVENTS
    public static event Action OnStageStart;
    public static event Action OnStageEnd;
    #endregion


    private void Start()
    {
        // Yield one frame before starting the stage.
        // This guarantees all other Start() methods in the scene have completed —
        // Sc_SceneUIBinder has registered the HUD, Mb_HealthComponent has initialized,
        // and all event subscriptions are in place before we fire OnStageStart.
        // Without this, Start() execution order between objects is not guaranteed,
        // causing the HUD to miss the Playing state change in builds.
        StartCoroutine(InitializeAfterSceneReady());
    }


    private IEnumerator InitializeAfterSceneReady()
    {
        // One frame is enough — Unity guarantees all Start() calls complete
        // within the first frame of a scene being active.
        yield return null;

        StartStage();
    }


    public SO_StageData GetStageData()
    {
        return _CurrentStageData;
    }


    private void StartStage()
    {
        Debug.Log("Start Stage");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Mb_StatBlock guardianStats = player.GetComponent<Mb_StatBlock>();
            if (guardianStats != null && Mb_AlmanacManager.Instance != null)
                Mb_AlmanacManager.Instance.ReapplyAllBonuses(guardianStats);
        }

        GameManager.Instance.ChangeState(GameState.Playing);
        OnStageStart?.Invoke();
    }


    public void EndStage()
    {
        OnStageEnd?.Invoke();
    }


    private void OnEnable()
    {
        GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState newState)
    {
        //bool showCursor = newState == GameState.RewardsPanel || newState == GameState.Paused || newState == GameState.Defeat || newState == GameState.Victory;
        //Cursor.visible = showCursor;
        //Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
    }
}