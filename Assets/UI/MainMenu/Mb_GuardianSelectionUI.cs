// Mb_GuardianSelectionUI.cs
// Drives the Guardian Selection screen.
// Lives on the GuardianSelectionCanvas GameObject.
//
// SCREEN LAYOUT (set up in the Unity Editor):
//
//   GuardianSelectionCanvas
//   ├── LeftPanel
//   │   ├── TitleText                   (TMP_Text — "Guardian Select")
//   │   ├── PortraitImage               (Image — SO_Guardian.icon)
//   │   └── SelectorRow
//   │       ├── SelectorButton[0]       (Button — Rajah)
//   │       └── SelectorButton[1]       (Button — Mari)
//   └── RightPanel
//       ├── GuardianNameText            (TMP_Text — SO_Guardian.CharacterName)
//       ├── ArchetypeRow
//       │   ├── Archetype1Text          (TMP_Text — SO_Guardian.GuardianArchetype1)
//       │   └── Archetype2Text          (TMP_Text — SO_Guardian.GuardianArchetype2)
//       ├── AbilityGrid
//       │   ├── Left Column             (Passive, Q, E — larger icons, label below)
//       │   │   ├── AbilitySlot[0]      Passive
//       │   │   ├── AbilitySlot[1]      Q
//       │   │   └── AbilitySlot[2]      E
//       │   └── Right Column            (R1/Primary top row, R2/Secondary bottom row)
//       │       ├── AbilitySlot[3]      R1
//       │       ├── AbilitySlot[4]      Primary
//       │       ├── AbilitySlot[5]      R2
//       │       └── AbilitySlot[6]      Secondary
//       ├── AbilityNameText             (TMP_Text — selected SO_Ability.AbilityName)
//       ├── AbilityDescriptionText      (TMP_Text — selected SO_Ability.AbilityDescription)
//       ├── LetsGoButton                (Button — confirms and loads stage)
//       └── BackButton                  (Button — returns to Stage Selection)
//
// ABILITY SLOT ORDER (maps to SO_Guardian fields):
//   [0] Passive    → PassiveAbility
//   [1] Q          → AbilityQ
//   [2] E          → AbilityE
//   [3] R1         → AbilityR_Branch1
//   [4] Primary    → PrimaryAttack
//   [5] R2         → AbilityR_Branch2
//   [6] Secondary  → SecondaryAttack
//
// HOW IT CONNECTS:
//   - All display data sourced from SO_Guardian → SO_Ability chain
//   - On confirm: sets Sc_RunSession.SelectedGuardian, calls SceneLoader.LoadStage()
//   - ResetToDefault() called by Mb_MainMenuController on every screen open
//
// Inspector setup:
//   - GuardianEntries: assign SO_Guardian assets — Index 0 = Rajah, Index 1 = Mari
//   - AbilitySlots: assign 7 Mb_AbilitySlotUI components in the order listed above
//   - SelectorButtons: assign 2 buttons matching GuardianEntries order
//   - Assign all TMP_Text and Image references
//   - Assign MainMenuController reference

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_GuardianSelectionUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    #region Inspector Fields        //----------------------------------------

    [Header("Guardian Data")]
    [Tooltip("SO_Guardian assets — Index 0 = Rajah (default), Index 1 = Mari. " +
             "Order must match SelectorButtons order.")]
    [SerializeField] private SO_Guardian[] GuardianEntries;

    [Header("Left Panel")]
    [Tooltip("Large portrait image — populated from SO_Guardian.icon.")]
    [SerializeField] private Image PortraitImage;

    [Tooltip("Selector buttons at the bottom of the left panel. " +
             "Must match GuardianEntries order.")]
    [SerializeField] private Button[] SelectorButtons;

    [Tooltip("Color tint on the selector button of the active guardian.")]
    [SerializeField] private Color SelectedSelectorColor = new Color(0.9f, 0.8f, 0.4f, 1f);

    [Tooltip("Color tint on inactive selector buttons.")]
    [SerializeField] private Color UnselectedSelectorColor = Color.white;

    [Header("Right Panel — Identity")]
    [Tooltip("Large guardian name label — SO_Guardian.CharacterName.")]
    [SerializeField] private TMP_Text GuardianNameText;

    [Tooltip("Left archetype tag — SO_Guardian.GuardianArchetype1.")]
    [SerializeField] private TMP_Text Archetype1Text;

    [Tooltip("Right archetype tag — SO_Guardian.GuardianArchetype2.")]
    [SerializeField] private TMP_Text Archetype2Text;

    [Header("Right Panel — Ability Grid")]
    [Tooltip("Seven ability slot components in this exact order: " +
             "[0] Passive, [1] Q, [2] E, [3] R1, [4] Primary, [5] R2, [6] Secondary. " +
             "Each slot is an Mb_AbilitySlotUI component in the scene.")]
    [SerializeField] private Mb_SelectionAbilitySlotUI[] AbilitySlots = new Mb_SelectionAbilitySlotUI[7];

    [Tooltip("Color tint applied to the currently selected ability slot icon.")]
    [SerializeField] private Color SelectedSlotColor = new Color(0.4f, 1f, 0.6f, 1f);

    [Tooltip("Color tint on unselected ability slot icons.")]
    [SerializeField] private Color UnselectedSlotColor = Color.white;

    [Header("Right Panel — Ability Detail")]
    [Tooltip("Label showing the selected ability's name — SO_Ability.AbilityName.")]
    [SerializeField] private TMP_Text AbilityNameText;

    [Tooltip("Text block showing the selected ability's description — " +
             "SO_Ability.AbilityDescription.")]
    [SerializeField] private TMP_Text AbilityDescriptionText;

    [Header("Action Buttons")]
    [Tooltip("Confirm button — stores session data and loads the stage.")]
    [SerializeField] private Button LetsGoButton;

    [Tooltip("Back button — returns to Stage Selection.")]
    [SerializeField] private Button BackButton;

    [Header("References")]
    [Tooltip("The main menu controller — used for Back navigation.")]
    [SerializeField] private Mb_MainMenuController MainMenuController;

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    #region Runtime State           //----------------------------------------

    // Which guardian is currently displayed (index into GuardianEntries)
    private int _selectedGuardianIndex = 0;

    // Which ability slot is currently selected (index into AbilitySlots)
    // Defaults to 0 (Passive) on every screen open
    private int _selectedAbilityIndex = 0;

    // Cached SO_Ability array built from the active SO_Guardian each time
    // the guardian selection changes — avoids re-fetching every click
    private SO_Ability[] _currentAbilities = new SO_Ability[7];

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        if (GuardianEntries == null || GuardianEntries.Length == 0)
            Debug.LogError("[Mb_GuardianSelectionUI] GuardianEntries is empty. " +
                           "Assign SO_Guardian assets in the Inspector.");

        if (AbilitySlots.Length != 7)
            Debug.LogError("[Mb_GuardianSelectionUI] AbilitySlots must have exactly " +
                           "7 entries. Check the Inspector.");

        if (SelectorButtons == null || SelectorButtons.Length != GuardianEntries?.Length)
            Debug.LogError("[Mb_GuardianSelectionUI] SelectorButtons count must match " +
                           "GuardianEntries count.");

        if (MainMenuController == null)
            Debug.LogError("[Mb_GuardianSelectionUI] MainMenuController is not assigned.");
    }


    private void OnEnable()
    {
        // Wire selector button callbacks — unsubscribe first to prevent stacking
        if (SelectorButtons != null)
        {
            for (int i = 0; i < SelectorButtons.Length; i++)
            {
                if (SelectorButtons[i] == null) continue;

                // Capture the loop variable so each lambda closes over
                // its own index, not the shared loop variable
                int index = i;
                SelectorButtons[i].onClick.RemoveAllListeners();
                SelectorButtons[i].onClick.AddListener(() => OnSelectorClicked(index));
            }
        }

        // Wire ability slot callbacks
        for (int i = 0; i < AbilitySlots.Length; i++)
        {
            if (AbilitySlots[i] == null) continue;

            int index = i;
            AbilitySlots[i].SetClickCallback(() => OnAbilitySlotClicked(index));
        }

        LetsGoButton?.onClick.RemoveAllListeners();
        LetsGoButton?.onClick.AddListener(OnLetsGoClicked);

        BackButton?.onClick.RemoveAllListeners();
        BackButton?.onClick.AddListener(OnBackClicked);
    }


    private void OnDisable()
    {
        // Listeners are cleared in OnEnable via RemoveAllListeners —
        // no additional cleanup needed here
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    #region Public API              //----------------------------------------

    /// <summary>
    /// Resets the screen to its default state:
    ///   - First guardian (Rajah) selected
    ///   - Passive ability selected and displayed
    /// Called by Mb_MainMenuController.ShowGuardianSelection() on every open
    /// so the screen never shows a stale selection from a previous visit.
    /// </summary>
    public void ResetToDefault()
    {
        _selectedGuardianIndex = 0;
        _selectedAbilityIndex = 0;

        RefreshGuardianDisplay();
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Guardian Selection
    // -------------------------------------------------------------------------

    #region Guardian Selection      //----------------------------------------

    private void OnSelectorClicked(int index)
    {
        if (GuardianEntries == null || index < 0 || index >= GuardianEntries.Length)
        {
            Debug.LogWarning($"[Mb_GuardianSelectionUI] Invalid guardian index: {index}.");
            return;
        }

        // Reset ability selection to Passive whenever the guardian changes
        // so we never show an ability from the previous guardian
        _selectedGuardianIndex = index;
        _selectedAbilityIndex = 0;

        RefreshGuardianDisplay();
    }


    // Rebuilds every UI element for the currently selected guardian.
    // Called after guardian changes and from ResetToDefault().
    private void RefreshGuardianDisplay()
    {
        SO_Guardian guardian = GetSelectedGuardian();
        if (guardian == null) return;

        PopulateIdentity(guardian);
        BuildAbilityCache(guardian);
        RefreshAbilitySlotIcons();
        RefreshSelectorHighlights();
        ShowAbilityDetail(_selectedAbilityIndex);
    }


    private void PopulateIdentity(SO_Guardian guardian)
    {
        // Portrait
        if (PortraitImage != null)
        {
            bool hasPortrait = guardian.icon != null;
            PortraitImage.sprite = hasPortrait ? guardian.icon : null;
            PortraitImage.gameObject.SetActive(hasPortrait);

            // TODO: Show a placeholder silhouette sprite here once art is ready
            //       rather than hiding the image entirely
        }

        // Name
        if (GuardianNameText != null)
            GuardianNameText.text = guardian.CharacterName;

        // Archetypes
        if (Archetype1Text != null)
            Archetype1Text.text = guardian.GuardianArchetype1;

        if (Archetype2Text != null)
            Archetype2Text.text = guardian.GuardianArchetype2;
    }


    // Builds the flat ability array from the guardian SO so ability slots
    // can be accessed by index without a switch statement every click.
    // Order matches the Inspector slot assignment documented at the top.
    private void BuildAbilityCache(SO_Guardian guardian)
    {
        _currentAbilities = new SO_Ability[]
        {
            guardian.PassiveAbility,        // [0] Passive
            guardian.AbilityQ,              // [1] Q
            guardian.AbilityE,              // [2] E
            guardian.AbilityR_Branch1,      // [3] R1
            guardian.PrimaryAttack,         // [4] Primary
            guardian.AbilityR_Branch2,      // [5] R2
            guardian.SecondaryAttack        // [6] Secondary
        };
    }


    // Pushes ability icons into each slot UI based on the current ability cache.
    // Called after BuildAbilityCache() so slots always show the correct guardian's abilities.
    private void RefreshAbilitySlotIcons()
    {
        for (int i = 0; i < AbilitySlots.Length; i++)
        {
            if (AbilitySlots[i] == null) continue;

            SO_Ability ability = GetAbilityAt(i);
            bool isSelected = i == _selectedAbilityIndex;

            Sprite icon = ability != null ? ability.Icon : null;
            Color tint = isSelected ? SelectedSlotColor : UnselectedSlotColor;

            AbilitySlots[i].SetIcon(icon, tint);
        }
    }


    // Updates selector button tints to highlight the active guardian.
    private void RefreshSelectorHighlights()
    {
        if (SelectorButtons == null) return;

        for (int i = 0; i < SelectorButtons.Length; i++)
        {
            if (SelectorButtons[i] == null) continue;

            Image btnImage = SelectorButtons[i].GetComponent<Image>();
            if (btnImage == null) continue;

            btnImage.color = i == _selectedGuardianIndex
                ? SelectedSelectorColor
                : UnselectedSelectorColor;
        }
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Ability Selection
    // -------------------------------------------------------------------------

    #region Ability Selection       //----------------------------------------

    private void OnAbilitySlotClicked(int index)
    {
        if (index < 0 || index >= AbilitySlots.Length) return;

        _selectedAbilityIndex = index;

        // Refresh icon tints so the newly selected slot highlights
        // and all others revert to unselected
        RefreshAbilitySlotIcons();

        // Update the name and description panel below the grid
        ShowAbilityDetail(index);
    }


    // Populates the Ability Name and Description labels from the selected ability.
    // Shows placeholder text if the SO_Ability reference is null so the
    // detail panel is never left blank.
    private void ShowAbilityDetail(int index)
    {
        SO_Ability ability = GetAbilityAt(index);

        if (AbilityNameText != null)
        {
            AbilityNameText.text = ability != null
                ? ability.AbilityName
                : "—";
        }

        if (AbilityDescriptionText != null)
        {
            AbilityDescriptionText.text = ability != null
                ? ability.AbilityDescription
                : "No description available.";

            // TODO: If AbilityDescription is not yet filled on the SO_Ability asset,
            //       the fallback above will show. Fill descriptions on each SO before
            //       this screen goes into the build.
        }
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Action Button Handlers
    // -------------------------------------------------------------------------

    #region Action Button Handlers  //----------------------------------------

    private void OnLetsGoClicked()
    {
        SO_Guardian guardian = GetSelectedGuardian();

        if (guardian == null)
        {
            Debug.LogError("[Mb_GuardianSelectionUI] No valid guardian selected.");
            return;
        }

        // Store the confirmed guardian so the stage scene can read it
        Sc_RunSession.SelectedGuardian = guardian;

        // SelectedStageNumber was already set by Mb_StageSelectionUI —
        // validate the full session before loading
        if (!Sc_RunSession.IsValid())
        {
            Debug.LogError("[Mb_GuardianSelectionUI] Sc_RunSession is not valid. " +
                           "Ensure SelectedStageNumber was set by Mb_StageSelectionUI.");
            return;
        }

        Debug.Log($"[Mb_GuardianSelectionUI] Confirmed — " +
                  $"Guardian: {guardian.CharacterName}, " +
                  $"Stage: {Sc_RunSession.SelectedStageNumber}. Loading route.");

        gameObject.SetActive(false);

        if (Sc_CutsceneSession.ConsumePendingStageCutscene())
        {
            SceneLoader.Instance.LoadCutscene();
            return;
        }

        if (Sc_RunSession.SelectedStageNumber == Sc_RunSession.TUTORIAL_STAGE)
        {
            // Load the tutorial scene instead of the normal stage scene
            SceneLoader.Instance.LoadTutorial();
            return;
        }
        SceneLoader.Instance.LoadStage(Sc_RunSession.SelectedStageNumber);
    }


    private void OnBackClicked()
    {
        // Clear only the guardian — stage number stays set since the
        // player is only going back one screen, not all the way to the menu
        Sc_RunSession.SelectedGuardian = null;
        Sc_CutsceneSession.ClearPendingCutscene();

        MainMenuController?.ShowStageSelection();
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    #region Helpers                 //----------------------------------------

    private SO_Guardian GetSelectedGuardian()
    {
        if (GuardianEntries == null || _selectedGuardianIndex >= GuardianEntries.Length)
            return null;

        SO_Guardian guardian = GuardianEntries[_selectedGuardianIndex];

        if (guardian == null)
            Debug.LogWarning($"[Mb_GuardianSelectionUI] " +
                             $"GuardianEntries[{_selectedGuardianIndex}] is null.");

        return guardian;
    }


    private SO_Ability GetAbilityAt(int index)
    {
        if (_currentAbilities == null || index < 0 || index >= _currentAbilities.Length)
            return null;

        return _currentAbilities[index];
    }

    #endregion                      //----------------------------------------
}
