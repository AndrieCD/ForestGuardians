// Mb_TutorialGateManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mb_TutorialGateManager : MonoBehaviour
{
    #region Inspector Fields        //----------------------------------------

    [Header("References")]
    [Tooltip("Drag the Mb_WaveManager component here.")]
    [SerializeField] private Mb_WaveManager _WaveManager;

    [Header("Dialog Sequences — Gates")]
    [SerializeField] private SO_DialogSequence IntroSequence;
    [SerializeField] private SO_DialogSequence MovementSequence;
    [SerializeField] private SO_DialogSequence JumpSequence;
    [SerializeField] private SO_DialogSequence AttackSequence;
    [SerializeField] private SO_DialogSequence AbilitySequence;
    [SerializeField] private SO_DialogSequence WaveReadySequence;

    [Tooltip("Diya explains the Almanac and Wildlife Discovery. " +
             "Plays after all waves clear, before the closing sequence.")]
    [SerializeField] private SO_DialogSequence WildlifeSequence;
    [SerializeField] private SO_DialogSequence ClosingSequence;

    [Header("Post-Reward Dialog Sequences")]
    [Tooltip("Index 0 = after ability upgrade, 1 = after augment, 2 = after ult branch.")]
    [SerializeField]
    private List<SO_DialogSequence> PostRewardSequences
        = new List<SO_DialogSequence>();

    [Header("Wave 4 Practice")]
    [Tooltip("How long the player practices with the 3 dummies before closing dialog plays.")]
    [SerializeField] private float Wave4PracticeDuration = 30f;

    #endregion                      //----------------------------------------


    #region Private State           //----------------------------------------

    // Resolved dynamically via OnActiveGuardianChanged — not an Inspector field
    private Mb_PlayerController _playerController;

    private int _currentGate = 0;

    private bool _listeningForJump = false;
    private bool _listeningForPrimary = false;
    private bool _listeningForAbility = false;

    private int _wavesCleared = 0;
    private bool _combatGatesActive = false;
    private bool _closingStarted = false;

    // Gate sequence coroutine is started on stage start but needs the
    // guardian to be bound first — this flag lets us re-check on bind
    private bool _gateSequenceStarted = false;

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        if (_WaveManager == null)
            Debug.LogError("[Mb_TutorialGateManager] WaveManager is not assigned.");
    }


    private void OnEnable()
    {
        Mb_GuardianBase.OnActiveGuardianChanged += HandleActiveGuardianChanged;
        Mb_StageManager.OnStageStart += HandleStageStart;
        Mb_WaveManager.OnWaveStart += HandleWaveStart;
        Mb_WaveManager.OnWaveEnd += HandleWaveEnd;
        Mb_Movement.OnLanded += HandlePlayerLanded;
        Mb_AbilityController.OnAnyAbilityActivated += HandleAbilityActivated;
        Mb_RewardsManager.OnRewardsPanelClosed += HandleRewardsPanelClosed;

        // Bind immediately if a guardian is already active when this enables
        if (Mb_GuardianBase.CurrentGuardian != null)
            BindGuardian(Mb_GuardianBase.CurrentGuardian);
    }


    private void OnDisable()
    {
        Mb_GuardianBase.OnActiveGuardianChanged -= HandleActiveGuardianChanged;
        Mb_StageManager.OnStageStart -= HandleStageStart;
        Mb_WaveManager.OnWaveStart -= HandleWaveStart;
        Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;
        Mb_Movement.OnLanded -= HandlePlayerLanded;
        Mb_AbilityController.OnAnyAbilityActivated -= HandleAbilityActivated;
        Mb_RewardsManager.OnRewardsPanelClosed -= HandleRewardsPanelClosed;
    }

    #endregion                      //----------------------------------------


    #region Guardian Binding        //----------------------------------------

    private void HandleActiveGuardianChanged(Mb_GuardianBase guardian)
    {
        BindGuardian(guardian);
    }


    private void BindGuardian(Mb_GuardianBase guardian)
    {
        _playerController = null;

        if (guardian == null)
        {
            Debug.LogWarning("[Mb_TutorialGateManager] BindGuardian called with null guardian.");
            return;
        }

        _playerController = guardian.GetComponent<Mb_PlayerController>();

        if (_playerController == null)
            Debug.LogError($"[Mb_TutorialGateManager] No Mb_PlayerController found on " +
                           $"{guardian.gameObject.name}.");
        else
            Debug.Log($"[Mb_TutorialGateManager] Guardian bound: {guardian.gameObject.name}");
    }

    #endregion                      //----------------------------------------


    #region Gate Sequence           //----------------------------------------

    private void HandleStageStart()
    {
        // If guardian isn't bound yet, wait — OnActiveGuardianChanged will
        // fire shortly after and _playerController will be set.
        // We start the coroutine regardless; SetAllActionsDisabled guards against null.
        SetAllActionsDisabled(true);
        _currentGate = 0;
        _gateSequenceStarted = true;
        StartCoroutine(RunGateSequence());
    }


    private IEnumerator RunGateSequence()
    {
        // Wait until guardian is bound before proceeding —
        // stage start may fire before OnActiveGuardianChanged in some load orders
        yield return new WaitUntil(() => _playerController != null);

        // Re-apply disable flags now that we have the controller
        SetAllActionsDisabled(true);

        // ── Gate 0: Intro ──────────────────────────────────────────────────
        _currentGate = 0;
        yield return StartCoroutine(PlaySequenceAndWait(IntroSequence));

        // ── Gate 1: Movement ───────────────────────────────────────────────
        _currentGate = 1;
        _playerController?.RemoveDisable(ActionDisableFlags.Movement);
        _playerController?.RemoveDisable(ActionDisableFlags.Rotation);
        yield return StartCoroutine(PlaySequenceAndWait(MovementSequence));

        // ── Gate 2: Jump ───────────────────────────────────────────────────
        _currentGate = 2;
        _playerController?.RemoveDisable(ActionDisableFlags.Jump);
        yield return StartCoroutine(PlaySequenceAndWait(JumpSequence));
        _listeningForJump = true;
        yield return new WaitUntil(() => !_listeningForJump);

        // ── Gate 3: Basic Attacks ──────────────────────────────────────────
        _currentGate = 3;
        _playerController?.RemoveDisable(ActionDisableFlags.PrimaryAttack);
        _playerController?.RemoveDisable(ActionDisableFlags.SecondaryAttack);
        yield return StartCoroutine(PlaySequenceAndWait(AttackSequence));
        _listeningForPrimary = true;
        yield return new WaitUntil(() => !_listeningForPrimary);

        // ── Gate 4: Abilities ──────────────────────────────────────────────
        _currentGate = 4;
        _playerController?.RemoveDisable(ActionDisableFlags.AbilityQ);
        _playerController?.RemoveDisable(ActionDisableFlags.AbilityE);
        _playerController?.RemoveDisable(ActionDisableFlags.Dash);
        yield return StartCoroutine(PlaySequenceAndWait(AbilitySequence));
        _listeningForAbility = true;
        yield return new WaitUntil(() => !_listeningForAbility);

        // ── Gate 5: Wave Ready ─────────────────────────────────────────────
        _currentGate = 5;
        SetAllActionsDisabled(false);
        yield return StartCoroutine(PlaySequenceAndWait(WaveReadySequence));

        _combatGatesActive = true;
        if (_WaveManager != null)
            _WaveManager.HoldWaves = false;

        Debug.Log("[Mb_TutorialGateManager] All pre-wave gates complete. " +
                  "Wave manager released.");
    }


    private IEnumerator PlaySequenceAndWait(SO_DialogSequence sequence)
    {
        Debug.Log($"[Mb_TutorialGateManager] Playing Gate {_currentGate} sequence: " +
                  $"{sequence?.name ?? "null"}");

        if (sequence == null) yield break;
        if (Mb_DialogManager.Instance == null) yield break;

        Mb_DialogManager.Instance.EnqueueSequence(sequence);

        yield return new WaitForSeconds(0.3f);

        while (Mb_DialogManager.Instance.IsDialogActive)
            yield return new WaitForSeconds(0.1f);
    }

    #endregion                      //----------------------------------------


    #region Event Handlers          //----------------------------------------

    private void HandlePlayerLanded()
    {
        if (!_listeningForJump) return;
        _listeningForJump = false;
        Debug.Log("[Mb_TutorialGateManager] Gate 2 complete — player jumped.");
    }


    private void HandleAbilityActivated(string slotName)
    {
        if (_listeningForPrimary && slotName == "Primary")
        {
            _listeningForPrimary = false;
            Debug.Log("[Mb_TutorialGateManager] Gate 3 complete — player used Primary.");
            return;
        }

        if (_listeningForAbility && (slotName == "Q" || slotName == "E"))
        {
            _listeningForAbility = false;
            Debug.Log("[Mb_TutorialGateManager] Gate 4 complete — player used ability.");
        }
    }


    private void HandleWaveStart(int waveIndex)
    {
        // Wave 4 (index 3) ends on a timer, not on kills,
        // because the practice dummies revive indefinitely
        if (waveIndex != 3) return;
        if (!_combatGatesActive) return;

        Debug.Log("[TutorialGate] Wave 4 started — beginning practice timer.");
        StartCoroutine(Wave4PracticeTimer());
    }


    private void HandleWaveEnd(int completedWaveIndex)
    {
        if (!_combatGatesActive) return;
        if (_closingStarted) return;

        _wavesCleared++;

        Debug.Log($"[TutorialGate] HandleWaveEnd fired — index={completedWaveIndex}, " +
                  $"_wavesCleared={_wavesCleared}");
    }


    private void HandleRewardsPanelClosed()
    {
        if (!_combatGatesActive) return;

        int rewardIndex = _wavesCleared - 1;

        Debug.Log($"[TutorialGate] RewardsPanelClosed — rewardIndex={rewardIndex}");

        if (rewardIndex >= 0
            && rewardIndex < PostRewardSequences.Count
            && PostRewardSequences[rewardIndex] != null)
        {
            Mb_DialogManager.Instance?.EnqueueSequence(
                PostRewardSequences[rewardIndex]
            );
        }
    }


    private IEnumerator Wave4PracticeTimer()
    {
        yield return new WaitForSeconds(Wave4PracticeDuration);

        if (_closingStarted) yield break;
        _closingStarted = true;

        // Set synchronously before ForceEndCurrentWave so ResolutionRoutine
        // cannot race past the HoldFinalResolution check
        if (_WaveManager != null)
            _WaveManager.HoldFinalResolution = true;

        _WaveManager?.ForceEndCurrentWave();

        StartCoroutine(PlayClosingAndEndStage());
    }


    private IEnumerator PlayClosingAndEndStage()
    {
        yield return new WaitForSeconds(1.5f);

        yield return StartCoroutine(PlaySequenceAndWait(WildlifeSequence));

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(PlaySequenceAndWait(ClosingSequence));

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