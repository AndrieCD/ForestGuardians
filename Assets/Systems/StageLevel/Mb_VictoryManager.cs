// Mb_VictoryManager.cs
// Lives on the Stage GameObject alongside Mb_StageManager, Mb_WaveManager,
// Mb_DefeatManager, and Mb_RewardsManager.
//
// Listens for Mb_WaveManager.OnAllWavesCleared — fired when the final wave's
// last CuBot dies and there are no more wave SOs in the stage SO.
//
// WHY OnAllWavesCleared AND NOT OnStageEnd:
//   Mb_StageManager.OnStageEnd fires for BOTH victory and defeat — Mb_DefeatManager
//   calls _StageManager.EndStage() at the end of its sequence too.
//   OnAllWavesCleared fires ONLY when all waves are cleared, making it an
//   unambiguous victory condition with no false positives.
//
// DOUBLE-TRIGGER GUARD:
//   OnAllWavesCleared should only ever fire once per stage, but the guard flag
//   is kept as a safety net against any future edge case.
//
// INSPECTOR SETUP:
//   - Add Mb_VictoryManager to the Stage GameObject.
//   - VictoryScreen: drag the Mb_VictoryScreenUI component.
//   - StageManager: drag the Mb_StageManager component (inherited field).
//   - SequenceDuration: defaults to 4f real seconds (inherited field). Victory
//     sequences often feel better slightly longer — consider 5f.
//   - Message array is editable in the Inspector without touching code.

using UnityEngine;

public class Mb_VictoryManager : Mb_EndSequenceManager
{

    #region Inspector Fields            //----------------------------------------

    [Header("References")]
    [Tooltip("Drag the Mb_VictoryScreenUI component here.")]
    [SerializeField] private Mb_VictoryScreenUI _VictoryScreen;

    [Header("Victory Messages")]
    [Tooltip("One message is picked at random when all waves are cleared.")]
    [SerializeField]
    private string[] _VictoryMessages = new string[]
    {
        "Talihaya breathes again. The forest endures because of you.",
        "The CuBots have been driven back. The Panoharra stands strong.",
        "You have protected what matters most. The forest will remember this day.",
        "The machines retreat. The wild places of Talihaya are safe — for now."
    };

    #endregion                          //----------------------------------------


    #region Runtime State               //----------------------------------------

    private bool _isSequenceTriggered = false;

    #endregion                          //----------------------------------------


    #region Unity Lifecycle             //----------------------------------------

    private void Start()
    {
        if (_VictoryScreen == null)
            Debug.LogError("[Mb_VictoryManager] VictoryScreen is not assigned in the Inspector.");

        if (_StageManager == null)
            Debug.LogWarning("[Mb_VictoryManager] StageManager is not assigned. " +
                             "EndStage() will not be called after the victory sequence.");
    }


    private void OnEnable()
    {
        Mb_WaveManager.OnAllWavesCleared += HandleAllWavesCleared;
    }


    private void OnDisable()
    {
        Mb_WaveManager.OnAllWavesCleared -= HandleAllWavesCleared;
    }

    #endregion                          //----------------------------------------


    #region Victory Trigger             //----------------------------------------

    private void HandleAllWavesCleared()
    {
        if (_isSequenceTriggered) return;
        _isSequenceTriggered = true;

        Sc_EndSequenceConfig config = new Sc_EndSequenceConfig
        {
            // Victory uses full time speed — no slow-motion.
            // The dramatic weight comes from the music and message, not a slowdown.
            // Change to 0.5f here if a slow-motion victory feel is preferred.
            TimeScaleMultiplier = 1f,
            TargetGameState = GameState.Victory,
            Screen = _VictoryScreen
        };

        StartCoroutine(RunEndSequence(config, PickRandom(_VictoryMessages)));
    }

    #endregion                          //----------------------------------------
}