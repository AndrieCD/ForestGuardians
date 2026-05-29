// Mb_EndSequenceManager.cs
// Abstract base MonoBehaviour that owns the shared end-of-game sequence coroutine.
//
// WHY THIS EXISTS:
//   Both Mb_DefeatManager and Mb_VictoryManager need to run the same dramatic
//   sequence: lock game state → adjust time → hold → restore time → show screen.
//   Extracting that logic here means it lives in exactly one place. If the
//   sequence timing or flow ever changes, one edit covers both outcomes.
//
// Derived classes:
//   Mb_DefeatManager  — subscribes to defeat triggers, builds a defeat config
//   Mb_VictoryManager — subscribes to OnAllWavesCleared, builds a victory config
//
// HOW TO ADD A NEW END-STATE:
//   1. Create a new MonoBehaviour that extends Mb_EndSequenceManager.
//   2. In Start(), cache references and subscribe to your trigger event.
//   3. In the trigger handler, build an Sc_EndSequenceConfig and call
//      StartCoroutine(RunEndSequence(config, message)).
//   Nothing in this base class needs to change.

using System.Collections;
using UnityEngine;

public abstract class Mb_EndSequenceManager : MonoBehaviour
{

    #region Inspector Fields            //----------------------------------------

    [Header("Sequence Timing")]
    [Tooltip("Real-world seconds to hold the time-scaled effect before showing the screen. " +
             "Uses WaitForSecondsRealtime — NOT affected by Time.timeScale.")]
    [SerializeField] protected float _SequenceDuration = 4f;

    [Header("Stage Reference")]
    [Tooltip("Drag the Mb_StageManager component here. EndStage() is called after the screen appears.")]
    [SerializeField] protected Mb_StageManager _StageManager;

    #endregion                          //----------------------------------------


    #region Core Sequence               //----------------------------------------

    /// <summary>
    /// The shared end-of-game sequence. Parameterized so defeat and victory
    /// use the same flow with different configs.
    ///
    /// Flow:
    ///   1. Lock game state (blocks input, abilities, pause)
    ///   2. Slow/adjust time (if configured)
    ///   3. Play SFX cue (TODO stub)
    ///   4. Hold for _SequenceDuration real-world seconds
    ///   5. Restore time
    ///   6. Show the end screen
    ///   7. Call EndStage() for cleanup
    /// </summary>
    /// <param name="config">Time scale, target state, and screen reference.</param>
    /// <param name="resolvedMessage">The exact message string to display.</param>
    protected IEnumerator RunEndSequence(Sc_EndSequenceConfig config, string resolvedMessage)
    {
        // 1. Transition game state to lock out player input, ability activation,
        //    and pause toggling. Mb_PauseManager guards against pausing outside
        //    GameState.Playing, so this also prevents the player from pausing mid-sequence.
        GameManager.Instance.ChangeState(config.TargetGameState);

        // 2. Adjust time scale for dramatic effect.
        //    Guard: Mb_PauseManager sets timeScale = 0 on pause. We must not override
        //    that with 0.5 — doing so would un-pause the game mid-sequence.
        //    If TimeScaleMultiplier == 1.0 (e.g. victory with no slow-mo), skip entirely.
        bool appliedTimeAdjust = false;
        if (!Mb_PauseManager.IsPaused && !Mathf.Approximately(config.TimeScaleMultiplier, 1f))
        {
            Time.timeScale = config.TimeScaleMultiplier;
            appliedTimeAdjust = true;
        }

        // 3. Audio cue — stubbed until AudioManager is implemented.
        // TODO: Replace with AudioManager.Instance.PlaySFX(config.SFXCue) or equivalent.
        Debug.Log($"[{GetType().Name}] End-sequence SFX — TODO: wire to AudioManager.");

        // 4. Hold for the configured duration in REAL-WORLD time.
        //    IMPORTANT: WaitForSecondsRealtime is mandatory here.
        //    At timeScale = 0.5, WaitForSeconds("4 seconds") would take 8 real seconds.
        //    WaitForSecondsRealtime ignores timeScale entirely.
        yield return new WaitForSecondsRealtime(_SequenceDuration);

        // 5. Restore time to normal BEFORE the screen appears.
        //    The UI fade and interactions must run at full speed.
        if (appliedTimeAdjust)
            Time.timeScale = 1f;

        // 6. Show the end screen.
        if (config.Screen != null)
            config.Screen.Show(resolvedMessage);
        else
            Debug.LogError($"[{GetType().Name}] Sc_EndSequenceConfig.Screen is null — " +
                           "assign the screen reference before calling RunEndSequence().");

        // 7. Trigger stage teardown AFTER the screen is visible.
        //    Mb_AugmentManager, Mb_WaveManager, and any other OnStageEnd listener
        //    will clean up their state here.
        _StageManager?.EndStage();
    }

    #endregion                          //----------------------------------------


    #region Helpers                     //----------------------------------------

    /// <summary>
    /// Picks one string at random from the given pool.
    /// Returns a safe fallback if the pool is null or empty.
    /// </summary>
    protected string PickRandom(string[] pool)
    {
        if (pool == null || pool.Length == 0)
            return "The stage has ended.";

        return pool[Random.Range(0, pool.Length)];
    }

    #endregion                          //----------------------------------------
}