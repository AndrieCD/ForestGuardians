// Mb_StageSelectionUI.cs
// Drives the Stage Selection screen — the stone tablet with gem nodes.
// Lives on the StageSelectionCanvas GameObject.
//
// SCREEN LAYOUT (set up in the Unity Editor):
//   StageSelectionCanvas
//   ├── TabletBackground        (Image — stone tablet art)
//   ├── GemNodes
//   │   ├── StageGem1           (Button + Mb_StageGemNode)
//   │   ├── CutsceneGem1        (Image — no button, greyed out)
//   │   ├── StageGem2           (Button + Mb_StageGemNode)
//   │   ├── CutsceneGem2        (Image — no button, greyed out)
//   │   └── StageGem3           (Button + Mb_StageGemNode)
//   ├── DetailPanel
//   │   ├── StageNameText       (TMP_Text)
//   │   └── StageDescriptionText(TMP_Text)
//   ├── SelectStageButton       (Button — disabled until a gem is selected)
//   └── BackButton              (Button — returns to main menu)
//
// GEM STATES:
//   Unlocked   — full color, interactable, shows name/description on click
//   Locked     — dimmed color, not interactable, shows locked indicator on click
//   Selected   — highlighted color, the stage that will load on confirm
//   Cutscene   — greyed out, no Button component, no interaction ever
//
// HOW IT CONNECTS:
//   - Reads unlock state from Mb_StageUnlockManager.Instance.IsUnlocked()
//   - On confirm: sets Sc_RunSession.SelectedStageNumber, then calls
//     Mb_MainMenuController.ShowGuardianSelection()
//   - Subscribes to Mb_StageUnlockManager.OnStageUnlocked to refresh gems
//     if the player returns to this screen after completing a stage
//
// Inspector setup:
//   - Assign all five Mb_StageGemNode references (3 stage gems, 2 cutscene nodes)
//   - Assign DetailPanel UI references
//   - Assign SelectStageButton and BackButton
//   - Assign MainMenuController reference for canvas switching
//   - Fill StageDetails array with one entry per stage (name + description)

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_StageSelectionUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    #region Inspector Fields        //----------------------------------------

    [Header("Gem Nodes — Stage")]
    [Tooltip("Gem node for Tutorial. Always unlocked.")]
    [SerializeField] private Mb_StageGemNode GemTutorial;

    [Tooltip("Gem node for Stage 1. Always unlocked.")]
    [SerializeField] private Mb_StageGemNode GemStage1;

    [Tooltip("Gem node for Stage 2. Unlocks after Stage 1 is completed.")]
    [SerializeField] private Mb_StageGemNode GemStage2;

    [Tooltip("Gem node for Stage 3. Unlocks after Stage 2 is completed.")]
    [SerializeField] private Mb_StageGemNode GemStage3;

    [Header("Gem Nodes — Cutscene")]
    [Tooltip("Cutscene node between Stage 1 and Stage 2. " +
             "No Button component — greyed out, non-interactive. " +
             "// TODO: Wire to cutscene playback once cutscenes are implemented.")]
    [SerializeField] private Image CutsceneGem1;

    [Tooltip("Cutscene node between Stage 2 and Stage 3. " +
             "// TODO: Wire to cutscene playback once cutscenes are implemented.")]
    [SerializeField] private Image CutsceneGem2;

    [Header("Detail Panel")]
    [Tooltip("TMP_Text that shows the selected stage's name.")]
    [SerializeField] private TMP_Text StageNameText;

    [Tooltip("TMP_Text that shows the selected stage's description.")]
    [SerializeField] private TMP_Text StageDescriptionText;

    [Header("Buttons")]
    [Tooltip("Confirm button — disabled until a stage gem is selected. " +
             "Advances to Guardian Selection.")]
    [SerializeField] private Button SelectStageButton;

    [Tooltip("Back button — returns to the main menu canvas.")]
    [SerializeField] private Button BackButton;

    [Header("References")]
    [Tooltip("The main menu controller — used for canvas switching.")]
    [SerializeField] private Mb_MainMenuController MainMenuController;

    [Header("Stage Details")]
    [Tooltip("Display data for each stage. Index 0 = Stage 1, 1 = Stage 2, 2 = Stage 3.")]
    [SerializeField] private StageDisplayData[] StageDetails = new StageDisplayData[3];

    [Header("Gem Colors")]
    [Tooltip("Color applied to an unlocked, unselected gem.")]
    [SerializeField] private Color UnlockedColor = new Color(0.9f, 0.8f, 0.4f, 1f);   // Gold

    [Tooltip("Color applied to the currently selected gem.")]
    [SerializeField] private Color SelectedColor = new Color(0.4f, 1f, 0.6f, 1f);     // Green

    [Tooltip("Color applied to a locked gem — dimmed to signal unavailability.")]
    [SerializeField] private Color LockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);     // Dark grey

    [Tooltip("Color applied to cutscene nodes — greyed out, non-interactive.")]
    [SerializeField] private Color CutsceneColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Translucent grey

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    #region Runtime State           //----------------------------------------

    // The stage number currently highlighted by the player (1, 2, 3, or 4 for tutorial).
    // 0 means nothing is selected yet — SelectStageButton stays disabled.
    private int _selectedStageNumber = 0;

    // Cached array of the three stage gem nodes for indexed access.
    // Built in Awake so RefreshGems() and OnGemClicked() can iterate them.
    private Mb_StageGemNode[] _stageGems;

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        // Cache gems into an array for indexed access.
        // Index 0 = Stage 1, 1 = Stage 2, 2 = Stage 3, 3 = Tutorial — matches StageDetails[].
        _stageGems = new Mb_StageGemNode[] { GemStage1, GemStage2, GemStage3, GemTutorial };

        //// Validate gem references so missing assignments surface immediately
        //for (int i = 0; i < _stageGems.Length; i++)
        //{
        //    if (_stageGems[i] == null)
        //        Debug.LogError($"[Mb_StageSelectionUI] GemStage{i + 1} is not assigned " +
        //                       "in the Inspector.");
        //}

        //if (SelectStageButton == null)
        //    Debug.LogError("[Mb_StageSelectionUI] SelectStageButton is not assigned.");

        if (MainMenuController == null)
            Debug.LogError("[Mb_StageSelectionUI] MainMenuController is not assigned.");

        //// Validate stage details array length
        //if (StageDetails.Length != 3)
        //    Debug.LogError("[Mb_StageSelectionUI] StageDetails must have exactly 3 entries " +
        //                   "(one per stage). Check the Inspector.");
    }


    private void OnEnable()
    {
        // Subscribe to unlock events so gem visuals refresh if a stage is
        // unlocked while this screen is open (e.g. returning from a stage victory)
        Mb_StageUnlockManager.OnStageUnlocked += HandleStageUnlocked;

        // Wire button callbacks — unsubscribe first as a safety net
        SelectStageButton?.onClick.RemoveAllListeners();
        SelectStageButton?.onClick.AddListener(OnSelectStageClicked);

        BackButton?.onClick.RemoveAllListeners();
        BackButton?.onClick.AddListener(OnBackClicked);

        // Wire gem click callbacks
        for (int i = 0; i < _stageGems.Length; i++)
        {
            // Capture loop variable for the lambda — without this all
            // lambdas would capture the same final value of i
            int stageNumber = i + 1;
            _stageGems[i]?.SetClickCallback(() => OnGemClicked(stageNumber));
        }

        RefreshGems();
        ClearDetailPanel();

        // Show stage details of selected stage when going back from Guardian selection
        if (Sc_RunSession.SelectedStageNumber != 0)
        {
            _selectedStageNumber = Sc_RunSession.SelectedStageNumber;
            ShowStageDetail(_selectedStageNumber);
        }
    }


    private void OnDisable()
    {
        Mb_StageUnlockManager.OnStageUnlocked -= HandleStageUnlocked;
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    #region Public API              //----------------------------------------

    /// <summary>
    /// Refreshes all gem visuals to reflect current unlock state.
    /// Called by Mb_MainMenuController.ShowStageSelection() every time
    /// the screen opens and by HandleStageUnlocked when a stage is newly unlocked.
    /// </summary>
    public void RefreshGems()
    {
        if (Mb_StageUnlockManager.Instance == null)
        {
            Debug.LogWarning("[Mb_StageSelectionUI] Mb_StageUnlockManager not found. " +
                             "Gem states cannot be refreshed.");
            return;
        }

        for (int i = 0; i < _stageGems.Length; i++)
        {
            if (_stageGems[i] == null) continue;

            int stageNumber = i + 1;
            bool isUnlocked = Mb_StageUnlockManager.Instance.IsUnlocked(stageNumber);
            bool isSelected = _selectedStageNumber == stageNumber;

            Color gemColor = isSelected
                ? SelectedColor
                : isUnlocked
                    ? UnlockedColor
                    : LockedColor;

            _stageGems[i].SetVisualState(isUnlocked, isSelected, gemColor);
        }

        // Cutscene nodes are always greyed out — no unlock state involved
        if (CutsceneGem1 != null) CutsceneGem1.color = CutsceneColor;
        if (CutsceneGem2 != null) CutsceneGem2.color = CutsceneColor;

        //// Show stage details of selected stage when going back from Guardian selection
        //if (Sc_RunSession.SelectedStageNumber != 0)
        //{
        //    _selectedStageNumber = Sc_RunSession.SelectedStageNumber;
        //    ShowStageDetail(_selectedStageNumber);
        //}

        // Keep the confirm button in sync with the current selection
        UpdateSelectButtonState();
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Gem Interaction
    // -------------------------------------------------------------------------

    #region Gem Interaction         //----------------------------------------

    private void OnGemClicked(int stageNumber)
    {
        if (Mb_StageUnlockManager.Instance == null) return;

        bool isUnlocked = Mb_StageUnlockManager.Instance.IsUnlocked(stageNumber);

        if (!isUnlocked)
        {
            // Show a locked indicator in the detail panel instead of stage info
            ShowLockedDetail();
            return;
        }

        // Select this stage — update state and refresh all gem visuals
        _selectedStageNumber = stageNumber;
        RefreshGems();
        ShowStageDetail(stageNumber);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Button Handlers
    // -------------------------------------------------------------------------

    #region Button Handlers         //----------------------------------------

    private void OnSelectStageClicked()
    {
        if (_selectedStageNumber <= 0)
        {
            Debug.LogWarning("[Mb_StageSelectionUI] Select Stage clicked with no " +
                             "stage selected — button should have been disabled.");
            return;
        }

        // Store the selected stage in the run session so GuardianSelection
        // and the stage scene can both read it
        Sc_RunSession.SelectedStageNumber = _selectedStageNumber;

        Debug.Log($"[Mb_StageSelectionUI] Stage {_selectedStageNumber} confirmed. " +
                  "Advancing to Guardian Selection.");

        MainMenuController?.ShowGuardianSelection();
    }


    private void OnBackClicked()
    {
        // Clear selection state when leaving so the screen resets on next open
        _selectedStageNumber = 0;
        ClearDetailPanel();

        MainMenuController?.ShowMainMenu();
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Detail Panel
    // -------------------------------------------------------------------------

    #region Detail Panel            //----------------------------------------

    private void ShowStageDetail(int stageNumber)
    {
        // stageNumber is 1-based; StageDetails array is 0-based
        int index = stageNumber - 1;

        if (index < 0 || index >= StageDetails.Length)
        {
            Debug.LogWarning($"[Mb_StageSelectionUI] No StageDetails entry for " +
                             $"stage {stageNumber}.");
            return;
        }

        if (StageNameText != null)
            StageNameText.text = StageDetails[index].StageName;

        if (StageDescriptionText != null)
            StageDescriptionText.text = StageDetails[index].StageDescription;
    }


    private void ShowLockedDetail()
    {
        if (StageNameText != null)
            StageNameText.text = "LOCKED";

        if (StageDescriptionText != null)
            StageDescriptionText.text = "Complete the previous stage to unlock.";
    }


    private void ClearDetailPanel()
    {
        if (StageNameText != null)
            StageNameText.text = string.Empty;

        if (StageDescriptionText != null)
            StageDescriptionText.text = "Select a stage.";
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    #region Helpers                 //----------------------------------------

    private void UpdateSelectButtonState()
    {
        if (SelectStageButton == null) return;

        // Button is only interactable when a valid, unlocked stage is selected
        bool canConfirm = _selectedStageNumber > 0
            && Mb_StageUnlockManager.Instance != null
            && Mb_StageUnlockManager.Instance.IsUnlocked(_selectedStageNumber);

        SelectStageButton.interactable = canConfirm;
    }


    private void HandleStageUnlocked(int stageNumber)
    {
        // A stage was just unlocked — refresh gems so the newly available
        // gem lights up without the player needing to close and reopen the screen
        RefreshGems();

        Debug.Log($"[Mb_StageSelectionUI] Stage {stageNumber} unlocked — gems refreshed.");
    }

    #endregion                      //----------------------------------------
}


// ─────────────────────────────────────────────────────────────────────────────
// Supporting Types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Display data for one stage shown in the detail panel when its gem is selected.
/// Fill these in the Inspector on Mb_StageSelectionUI.
/// </summary>
[Serializable]
public class StageDisplayData
{
    [Tooltip("Name shown in the detail panel when this stage is selected. " +
             "e.g. 'Stage 1 — The Lowland Forest'")]
    public string StageName;

    [Tooltip("Short description shown in the detail panel. " +
             "e.g. 'Defend the Panoharra from logging CuBots " +
             "across the river valleys of Panoharra.'")]
    [TextArea(2, 4)]
    public string StageDescription;
}
