// Mb_TutorialGateManager.cs
// Manages progressive action unlocking during the tutorial stage.
// Lives on the Stage GameObject alongside Mb_StageManager.
//
// HOW IT WORKS:
//   - On stage start, ALL player actions are disabled via ActionDisableFlags.
//   - Each gate follows this strict order:
//       1. Enable the action(s) for this gate
//       2. Enqueue the dialog sequence for this gate
//       3. Wait for dialog to fully finish
//       4. THEN arm the listener for the player action
//   - This prevents the player from accidentally advancing a gate by
//     performing an action during the dialog before the instruction line plays.
//   - Once all pre-wave gates clear, HoldWaves is set to false on Mb_WaveManager
//     so the wave system takes over naturally.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mb_TutorialGateManager : MonoBehaviour
{
    #region Inspector Fields        //----------------------------------------

    [Header("References")]
    [Tooltip("Drag the Guardian (Player) GameObject here.")]
    [SerializeField] private GameObject _PlayerObject;

    [Tooltip("Drag the Mb_WaveManager component here. " +
             "HoldWaves will be released when all pre-wave gates complete.")]
    [SerializeField] private Mb_WaveManager _WaveManager;

    [Header("Dialog Sequences — Gates")]
    [SerializeField] private SO_DialogSequence IntroSequence;
    [SerializeField] private SO_DialogSequence MovementSequence;
    [SerializeField] private SO_DialogSequence JumpSequence;
    [SerializeField] private SO_DialogSequence AttackSequence;
    [SerializeField] private SO_DialogSequence AbilitySequence;
    [SerializeField] private SO_DialogSequence WaveReadySequence;
    [SerializeField] private SO_DialogSequence ClosingSequence;

    [Header("Post-Reward Dialog Sequences")]
    [Tooltip("Index 0 = after ability upgrade, 1 = after augment, 2 = after ult branch.")]
    [SerializeField]
    private List<SO_DialogSequence> PostRewardSequences
        = new List<SO_DialogSequence>();

    #endregion                      //----------------------------------------


    #region Private State           //----------------------------------------

    private Mb_PlayerController _playerController;

    // Which gate we are currently on — for logging and safety checks
    private int _currentGate = 0;

    // Armed by each gate AFTER dialog finishes.
    // The relevant event handler checks this before doing anything.
    private bool _listeningForJump = false;
    private bool _listeningForPrimary = false;
    private bool _listeningForAbility = false;

    // Tracks waves cleared for PostRewardSequences indexing
    private int _wavesCleared = 0;

    // True once pre-wave gates are all done
    private bool _combatGatesActive = false;

    // True once closing sequence has been triggered
    private bool _closingStarted = false;

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        if (_PlayerObject != null)
            _playerController = _PlayerObject.GetComponent<Mb_PlayerController>();

        if (_playerController == null)
            Debug.LogError("[Mb_TutorialGateManager] Could not find Mb_PlayerController " +
                           "on PlayerObject.");

        if (_WaveManager == null)
            Debug.LogError("[Mb_TutorialGateManager] WaveManager is not assigned. " +
                           "Drag the Mb_WaveManager component into the Inspector.");
    }


    private void OnEnable()
    {
        Mb_StageManager.OnStageStart += HandleStageStart;
        Mb_WaveManager.OnWaveEnd += HandleWaveEnd;
        Mb_Movement.OnLanded += HandlePlayerLanded;
        Mb_AbilityController.OnAnyAbilityActivated += HandleAbilityActivated;
        Mb_RewardsManager.OnRewardsPanelClosed += HandleRewardsPanelClosed;
    }


    private void OnDisable()
    {
        Mb_StageManager.OnStageStart -= HandleStageStart;
        Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;
        Mb_Movement.OnLanded -= HandlePlayerLanded;
        Mb_AbilityController.OnAnyAbilityActivated -= HandleAbilityActivated;
        Mb_RewardsManager.OnRewardsPanelClosed -= HandleRewardsPanelClosed;
    }

    #endregion                      //----------------------------------------


    #region Gate Sequence           //----------------------------------------

    private void HandleStageStart()
    {
        // Lock everything — player watches intro frozen
        SetAllActionsDisabled(true);
        _currentGate = 0;
        StartCoroutine(RunGateSequence());
    }


    // Master coroutine that runs all pre-wave gates in strict order.
    // Each gate waits for dialog to finish BEFORE arming its listener.
    private IEnumerator RunGateSequence()
    {


        // ── Gate 0: Intro ──────────────────────────────────────────────────
        // All actions disabled. Diya speaks. Auto-advances when dialog ends.
        _currentGate = 0;
        yield return StartCoroutine(PlaySequenceAndWait(IntroSequence));

        // ── Gate 1: Movement ───────────────────────────────────────────────
        // Enable movement and rotation. Dudong explains WASD. Auto-advances.
        _currentGate = 1;
        _playerController?.RemoveDisable(ActionDisableFlags.Movement);
        _playerController?.RemoveDisable(ActionDisableFlags.Rotation);
        yield return StartCoroutine(PlaySequenceAndWait(MovementSequence));

        // ── Gate 2: Jump ───────────────────────────────────────────────────
        // Enable jump. Dudong explains. Dialog finishes. THEN we listen.
        _currentGate = 2;
        _playerController?.RemoveDisable(ActionDisableFlags.Jump);
        yield return StartCoroutine(PlaySequenceAndWait(JumpSequence));
        // Dialog is done — now arm the listener
        _listeningForJump = true;
        yield return new WaitUntil(() => !_listeningForJump);

        // ── Gate 3: Basic Attacks ──────────────────────────────────────────
        // Enable primary and secondary. Dudong explains. Dialog finishes. THEN listen.
        _currentGate = 3;
        _playerController?.RemoveDisable(ActionDisableFlags.PrimaryAttack);
        _playerController?.RemoveDisable(ActionDisableFlags.SecondaryAttack);
        yield return StartCoroutine(PlaySequenceAndWait(AttackSequence));
        _listeningForPrimary = true;
        yield return new WaitUntil(() => !_listeningForPrimary);

        // ── Gate 4: Abilities ──────────────────────────────────────────────
        // Enable Q and E. Dudong explains. Dialog finishes. THEN listen.
        _currentGate = 4;
        _playerController?.RemoveDisable(ActionDisableFlags.AbilityQ);
        _playerController?.RemoveDisable(ActionDisableFlags.AbilityE);
        yield return StartCoroutine(PlaySequenceAndWait(AbilitySequence));
        _listeningForAbility = true;
        yield return new WaitUntil(() => !_listeningForAbility);

        // ── Gate 5: Wave Ready ─────────────────────────────────────────────
        // All actions unlocked. Dudong says waves are coming.
        // Release HoldWaves so Mb_WaveManager starts its preparation countdown.
        _currentGate = 5;
        SetAllActionsDisabled(false);
        yield return StartCoroutine(PlaySequenceAndWait(WaveReadySequence));

        // Release wave manager — it takes over from here
        _combatGatesActive = true;
        if (_WaveManager != null)
            _WaveManager.HoldWaves = false;

        Debug.Log("[Mb_TutorialGateManager] All pre-wave gates complete. " +
                  "Wave manager released.");
    }


    // Plays a dialog sequence and polls until it finishes.
    // Safe to call with a null sequence — just returns immediately.
    private IEnumerator PlaySequenceAndWait(SO_DialogSequence sequence)
    {
        Debug.Log($"[Mb_TutorialGateManager] Playing Gate {_currentGate} sequence: " +
                  $"{sequence?.name ?? "null"}");

        if (sequence == null) yield break;
        if (Mb_DialogManager.Instance == null) yield break;

        Mb_DialogManager.Instance.EnqueueSequence(sequence);

        // Brief buffer so IsDialogActive has time to become true
        // before we start polling
        yield return new WaitForSeconds(0.3f);

        while (Mb_DialogManager.Instance.IsDialogActive)
            yield return new WaitForSeconds(0.1f);
    }

    #endregion                      //----------------------------------------


    #region Event Handlers          //----------------------------------------

    private void HandlePlayerLanded()
    {
        Debug.Log($"[Mb_TutorialGateManager] Player landed. " +
                  $"Listening for jump: {_listeningForJump}");
        // Only respond if Gate 2 has armed this listener
        if (!_listeningForJump) return;

        _listeningForJump = false;
        Debug.Log("[Mb_TutorialGateManager] Gate 2 complete — player jumped.");
    }


    private void HandleAbilityActivated(string slotName)
    {
        Debug.Log($"[Mb_TutorialGateManager] Player activated ability in slot {slotName}. " +
                  $"Listening for primary: {_listeningForPrimary}, " +
                  $"Listening for ability: {_listeningForAbility}");
        // Gate 3 — waiting for primary attack
        if (_listeningForPrimary && slotName == "Primary")
        {
            _listeningForPrimary = false;
            Debug.Log("[Mb_TutorialGateManager] Gate 3 complete — player used Primary.");
            return;
        }

        // Gate 4 — waiting for Q or E
        if (_listeningForAbility && (slotName == "Q" || slotName == "E"))
        {
            _listeningForAbility = false;
            Debug.Log("[Mb_TutorialGateManager] Gate 4 complete — player used ability.");
        }
    }


    private void HandleWaveEnd(int completedWaveIndex)
    {
        if (!_combatGatesActive) return;
        if (_closingStarted) return;

        _wavesCleared++;

        // Wave 4 (index 3) is the last — trigger closing sequence
        if (completedWaveIndex >= 3)
        {
            _closingStarted = true;
            StartCoroutine(PlayClosingAndEndStage());
        }
    }


    private void HandleRewardsPanelClosed()
    {
        if (!_combatGatesActive) return;

        // _wavesCleared is incremented in HandleWaveEnd which fires before
        // the panel opens, so at this point it correctly reflects which
        // reward just closed (1 = ability upgrade, 2 = augment, 3 = ult branch)
        int rewardIndex = _wavesCleared - 1;

        if (rewardIndex >= 0
            && rewardIndex < PostRewardSequences.Count
            && PostRewardSequences[rewardIndex] != null)
        {
            Mb_DialogManager.Instance?.EnqueueSequence(
                PostRewardSequences[rewardIndex]
            );
        }
    }


    private IEnumerator PlayClosingAndEndStage()
    {
        // Hold the wave manager's final resolution so victory doesn't
        // fire before the closing dialog finishes
        if (_WaveManager != null)
            _WaveManager.HoldFinalResolution = true;

        yield return new WaitForSeconds(1.5f);

        yield return StartCoroutine(PlaySequenceAndWait(ClosingSequence));

        // Closing dialog done — release the hold so victory fires naturally
        if (_WaveManager != null)
            _WaveManager.HoldFinalResolution = false;
    }

    #endregion                      //----------------------------------------


    #region Helpers                 //----------------------------------------

    private void SetAllActionsDisabled(bool disabled)
    {
        if (_playerController == null) return;

        if (disabled)
        {
            _playerController.AddDisable(ActionDisableFlags.Movement);
            _playerController.AddDisable(ActionDisableFlags.Rotation);
            _playerController.AddDisable(ActionDisableFlags.Jump);
            _playerController.AddDisable(ActionDisableFlags.Dash);
            _playerController.AddDisable(ActionDisableFlags.PrimaryAttack);
            _playerController.AddDisable(ActionDisableFlags.SecondaryAttack);
            _playerController.AddDisable(ActionDisableFlags.AbilityQ);
            _playerController.AddDisable(ActionDisableFlags.AbilityE);
            _playerController.AddDisable(ActionDisableFlags.AbilityR);
        }
        else
        {
            _playerController.RemoveDisable(ActionDisableFlags.Movement);
            _playerController.RemoveDisable(ActionDisableFlags.Rotation);
            _playerController.RemoveDisable(ActionDisableFlags.Jump);
            _playerController.RemoveDisable(ActionDisableFlags.Dash);
            _playerController.RemoveDisable(ActionDisableFlags.PrimaryAttack);
            _playerController.RemoveDisable(ActionDisableFlags.SecondaryAttack);
            _playerController.RemoveDisable(ActionDisableFlags.AbilityQ);
            _playerController.RemoveDisable(ActionDisableFlags.AbilityE);
            _playerController.RemoveDisable(ActionDisableFlags.AbilityR);
        }
    }

    #endregion                      //----------------------------------------
}