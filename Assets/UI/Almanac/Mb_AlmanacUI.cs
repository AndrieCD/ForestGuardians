// Mb_AlmanacUI.cs
// Lives on the Almanac Canvas in the main menu scene.
// Drives the two-page book layout:
//   Left page  — diamond grid of all 13 entry cards + progress counter
//   Right page — detail panel for the selected entry
//
// FLAVOR TEXT ROTATION:
//   FlavourTexts is a list of strings assigned in the Inspector.
//   They cycle one at a time through FlavourText (TMP_Text) every
//   FlavorTextInterval seconds. Timer resets each time the Almanac opens.
//
// LAYOUT HIERARCHY (build this in the Unity Editor):
//
//   AlmanacCanvas (this GameObject)
//   ├── BookRoot
//   │   ├── LeftPage
//   │   │   ├── HeaderImage          (Image)
//   │   │   ├── FlavourText          (TMP_Text)
//   │   │   ├── DiamondGrid          (RectTransform — parent for all cards)
//   │   │   │   └── [DiamondCard x13] (instantiated from DiamondCardPrefab)
//   │   │   └── ProgressCounter      (TMP_Text — "X / 13")
//   │   └── RightPage
//   │       ├── ModelViewerPanel     (RawImage — displays RenderTexture for 3D model)
//   │       ├── NameBanner           (TMP_Text — CommonName or "???")
//   │       ├── StatBonusText        (TMP_Text — stat bonus line)
//   │       ├── InfoPanel
//   │       │   ├── LeftColumn
//   │       │   │   ├── ScientificNameText  (TMP_Text)
//   │       │   │   └── StatusText          (TMP_Text)
//   │       │   └── RightColumn
//   │       │       ├── PopulationText      (TMP_Text)
//   │       │       └── HabitatText         (TMP_Text)
//   │       ├── AboutHeader          (TMP_Text — "About the Animal" label)
//   │       ├── DescriptionText      (TMP_Text)
//   │       └── ReferencesText       (TMP_Text)
//   └── CloseButton                  (Button — X on right edge)
//
// Inspector setup:
//   - Assign all TMP_Text and Image references in the Inspector
//   - Assign DiamondCardPrefab — a prefab with Mb_AlmanacDiamondCard on its root
//   - Assign DiamondGrid as the parent RectTransform for card instantiation
//     Use a Grid Layout Group set to diamond arrangement, or position manually
//   - Assign ModelViewerRawImage — point its Texture to a RenderTexture asset
//   - Assign FlavourTexts — paste each flavor text string as a list element
//   - Set FlavorTextInterval — default 5 seconds, tune to taste
//   - Wire CloseButton.OnClick → Mb_AlmanacUI.OnCloseClicked in Inspector
//   - This Canvas starts inactive — activated by MainMenuController.OnAlmanacClicked()

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_AlmanacUI : MonoBehaviour
{

    #region Inspector Fields            //----------------------------------------

    [Header("References")]
    [Tooltip("The main menu controller — used for canvas switching.")]
    [SerializeField] private Mb_MainMenuController MainMenuController;

    [Header("Left Page")]
    [Tooltip("Image at the top of the left page — title art or logo.")]
    [SerializeField] private Image HeaderImage;

    [Tooltip("TMP_Text that displays the rotating flavor text. " +
             "Cycles through FlavourTexts every FlavorTextInterval seconds.")]
    [SerializeField] private TMP_Text FlavourText;

    [Tooltip("List of flavor text strings to rotate through. " +
             "Displayed one at a time in order, looping back to index 0. " +
             "Paste each educational flavor text as a separate list element here.")]
    [SerializeField] private List<string> FlavourTexts = new List<string>();

    [Tooltip("How many seconds each flavor text is shown before switching to the next. " +
             "Default: 5 seconds.")]
    // TODO: Tune this — 5s works for reading speed at ~100 WPM for 3-4 sentence texts.
    //       Raise to 7–8s if playtesting shows players don't finish reading.
    [SerializeField] private float FlavorTextInterval = 5f;

    [Tooltip("Parent RectTransform that diamond card prefabs are instantiated under. " +
             "Recommended: use a custom anchored layout or manually position cards " +
             "in a diamond/rotated grid pattern.")]
    [SerializeField] private RectTransform DiamondGrid;

    [Tooltip("Prefab for one diamond card. Must have Mb_AlmanacDiamondCard on its root.")]
    [SerializeField] private GameObject DiamondCardPrefab;

    [Tooltip("Shows 'X / 13' unlock progress at the bottom of the left page.")]
    [SerializeField] private TMP_Text ProgressCounterText;

    [Header("Right Page — 3D Viewer")]
    [Tooltip("RawImage that displays the RenderTexture from the model viewer camera. " +
             "Assign a RenderTexture asset to both this RawImage and the viewer Camera.")]
    [SerializeField] private RawImage ModelViewerRawImage;

    [Tooltip("GameObject that holds the model in the viewer scene. " +
             "Swap its child mesh when a new entry is selected.")]
    [SerializeField] private GameObject ModelViewerRoot;

    [Header("Right Page — Entry Detail")]
    [Tooltip("Large species name banner. Shows CommonName or '???' when locked.")]
    [SerializeField] private TMP_Text NameBannerText;

    [Tooltip("Stat bonus line. e.g. '??? → +20% Critical Damage' or '???' when locked.")]
    [SerializeField] private TMP_Text StatBonusText;

    [Header("Right Page — Info Fields")]
    [SerializeField] private TMP_Text ScientificNameText;
    [SerializeField] private TMP_Text StatusText;
    [SerializeField] private TMP_Text PopulationText;
    [SerializeField] private TMP_Text HabitatText;

    [Header("Right Page — About")]
    [Tooltip("'About the Animal' section description text.")]
    [SerializeField] private TMP_Text DescriptionText;

    [Tooltip("References field at the bottom of the right page.")]
    [SerializeField] private TMP_Text ReferencesText;

    [Header("Right Page — Empty State")]
    [Tooltip("Shown on the right page when no card has been selected yet.")]
    [SerializeField] private GameObject EmptyDetailPanel;

    [Tooltip("Root that holds all detail fields. Hidden until a card is selected.")]
    [SerializeField] private GameObject DetailContentPanel;

    [Header("Navigation")]
    [Tooltip("The X close button on the right edge. " +
             "Wire its OnClick to Mb_AlmanacUI.OnCloseClicked in the Inspector.")]
    [SerializeField] private Button CloseButton;

    #endregion                          //----------------------------------------


    #region Private State               //----------------------------------------

    // All instantiated diamond card components — kept so we can refresh them
    // when OnEntryUnlocked fires mid-session
    private List<Mb_AlmanacDiamondCard> _cards = new List<Mb_AlmanacDiamondCard>();

    // Maps each entry to its card for O(1) targeted refresh
    private Dictionary<SO_WildlifeEntry, Mb_AlmanacDiamondCard> _cardMap
        = new Dictionary<SO_WildlifeEntry, Mb_AlmanacDiamondCard>();

    // The entry whose detail is currently shown on the right page
    private SO_WildlifeEntry _selectedEntry;

    // Which flavor text is currently displayed — advances each interval
    private int _currentFlavorIndex = 0;

    // Accumulates delta time — when it exceeds FlavorTextInterval, we advance
    private float _flavorTimer = 0f;

    #endregion                          //----------------------------------------


    #region Unity Lifecycle             //----------------------------------------

    private void OnEnable()
    {
        Mb_AlmanacManager.OnEntryUnlocked += HandleEntryUnlocked;
        Mb_AlmanacManager.OnRepeatCompleted += HandleRepeatCompleted;

        BuildGrid();
        RefreshProgressCounter();
        ShowEmptyDetail();

        // Reset rotation to index 0 every time the Almanac opens
        // so the player always sees the first text on a fresh open
        _currentFlavorIndex = 0;
        _flavorTimer = 0f;
        ShowCurrentFlavourText();
    }

    private void OnDisable()
    {
        Mb_AlmanacManager.OnEntryUnlocked -= HandleEntryUnlocked;
        Mb_AlmanacManager.OnRepeatCompleted -= HandleRepeatCompleted;
    }

    private void Update()
    {
        // Only rotate if there is more than one text to cycle through
        if (FlavourTexts == null || FlavourTexts.Count <= 1) return;
        if (FlavorTextInterval <= 0f) return;

        _flavorTimer += Time.deltaTime;

        if (_flavorTimer >= FlavorTextInterval)
        {
            _flavorTimer = 0f;
            AdvanceFlavourText();
        }
    }

    #endregion                          //----------------------------------------


    #region Flavor Text Rotation        //----------------------------------------

    // Moves to the next flavor text in the list, wrapping back to 0 after the last.
    private void AdvanceFlavourText()
    {
        // Wrap around to index 0 after the last entry
        _currentFlavorIndex = (_currentFlavorIndex + 1) % FlavourTexts.Count;
        ShowCurrentFlavourText();
    }

    // Writes the current flavor text to the FlavourText TMP component.
    // Silently skips if the list is empty or the component is unassigned.
    private void ShowCurrentFlavourText()
    {
        if (FlavourText == null) return;
        if (FlavourTexts == null || FlavourTexts.Count == 0) return;

        FlavourText.text = FlavourTexts[_currentFlavorIndex];
    }

    #endregion                          //----------------------------------------


    #region Grid Building               //----------------------------------------

    private void BuildGrid()
    {
        if (Mb_AlmanacManager.Instance == null)
        {
            Debug.LogError("[Mb_AlmanacUI] Mb_AlmanacManager not found. " +
                           "Cannot build almanac grid.");
            return;
        }

        if (DiamondCardPrefab == null || DiamondGrid == null)
        {
            Debug.LogError("[Mb_AlmanacUI] DiamondCardPrefab or DiamondGrid not assigned.");
            return;
        }

        ClearGrid();

        foreach (Transform panel in DiamondGrid)
        {
            Mb_AlmanacPanelSlot slot = panel.GetComponent<Mb_AlmanacPanelSlot>();
            if (slot == null || slot.Entry == null) continue;

            SO_WildlifeEntry entry = slot.Entry;

            GameObject cardGO = Instantiate(DiamondCardPrefab, panel);
            Mb_AlmanacDiamondCard card = cardGO.GetComponent<Mb_AlmanacDiamondCard>();

            if (card == null)
            {
                Debug.LogError("[Mb_AlmanacUI] DiamondCardPrefab is missing " +
                               "Mb_AlmanacDiamondCard component.");
                continue;
            }

            RectTransform cardRect = cardGO.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = Vector2.zero;
                cardRect.anchorMax = Vector2.one;
                cardRect.offsetMin = Vector2.zero;
                cardRect.offsetMax = Vector2.zero;
            }

            bool isUnlocked = Mb_AlmanacManager.Instance.IsUnlocked(entry);
            int completionCount = Mb_AlmanacManager.Instance.GetCompletionCount(entry);

            card.Initialize(entry, isUnlocked, completionCount, OnCardClicked);

            _cards.Add(card);
            _cardMap[entry] = card;
        }

        Debug.Log($"[Mb_AlmanacUI] Grid built with {_cards.Count} entries.");
    }


    private void ClearGrid()
    {
        foreach (Transform panel in DiamondGrid)
        {
            foreach (Transform child in panel)
                Destroy(child.gameObject);
        }

        _cards.Clear();
        _cardMap.Clear();
    }

    #endregion                          //----------------------------------------


    #region Card Selection              //----------------------------------------

    private void OnCardClicked(SO_WildlifeEntry entry)
    {
        _selectedEntry = entry;

        bool isUnlocked = Mb_AlmanacManager.Instance != null
            && Mb_AlmanacManager.Instance.IsUnlocked(entry);

        PopulateDetailPanel(entry, isUnlocked);
        SwapModelViewer(entry, isUnlocked);
    }

    #endregion                          //----------------------------------------


    #region Detail Panel                //----------------------------------------

    private void PopulateDetailPanel(SO_WildlifeEntry entry, bool isUnlocked)
    {
        EmptyDetailPanel?.SetActive(false);
        DetailContentPanel?.SetActive(true);

        if (isUnlocked)
            PopulateUnlocked(entry);
        else
            PopulateLocked(entry);
    }


    private void PopulateUnlocked(SO_WildlifeEntry entry)
    {
        if (NameBannerText != null)
            NameBannerText.text = entry.CommonName;

        if (StatBonusText != null)
            StatBonusText.text = BuildStatBonusString(entry);

        if (ScientificNameText != null)
            ScientificNameText.text = $"Scientific Name: {entry.ScientificName}";

        if (StatusText != null)
            StatusText.text = $"Status: {BuildStatusString(entry.Status)}";

        if (PopulationText != null)
            PopulationText.text = $"Population: {entry.Population}";

        if (HabitatText != null)
            HabitatText.text = $"Habitat: {entry.Habitat}";

        if (DescriptionText != null)
            DescriptionText.text = entry.Description;

        if (ReferencesText != null)
            ReferencesText.text = "References:";
    }


    private void PopulateLocked(SO_WildlifeEntry entry)
    {
        if (NameBannerText != null)
            NameBannerText.text = "???";

        if (StatBonusText != null)
            StatBonusText.text = "???";

        if (ScientificNameText != null)
            ScientificNameText.text = "???";

        if (StatusText != null)
            StatusText.text = BuildStatusString(entry.Status);

        if (PopulationText != null)
            PopulationText.text = "???";

        if (HabitatText != null)
            HabitatText.text = "???";

        if (DescriptionText != null)
            DescriptionText.text = "???";

        if (ReferencesText != null)
            ReferencesText.text = "???";
    }


    private void ShowEmptyDetail()
    {
        _selectedEntry = null;
        EmptyDetailPanel?.SetActive(true);
        DetailContentPanel?.SetActive(false);
    }

    #endregion                          //----------------------------------------


    #region Model Viewer                //----------------------------------------

    private void SwapModelViewer(SO_WildlifeEntry entry, bool isUnlocked)
    {
        if (ModelViewerRoot == null) return;

        foreach (Transform child in ModelViewerRoot.transform)
            Destroy(child.gameObject);

        if (!isUnlocked || entry.ModelPrefab == null)
        {
            ModelViewerRawImage?.gameObject.SetActive(false);
            return;
        }

        ModelViewerRawImage?.gameObject.SetActive(true);

        GameObject model = Instantiate(entry.ModelPrefab, ModelViewerRoot.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
    }

    #endregion                          //----------------------------------------


    #region Progress Counter            //----------------------------------------

    private void RefreshProgressCounter()
    {
        if (ProgressCounterText == null) return;
        if (Mb_AlmanacManager.Instance == null) return;

        int total = 0;
        int unlocked = 0;

        foreach (Transform panel in DiamondGrid)
        {
            Mb_AlmanacPanelSlot slot = panel.GetComponent<Mb_AlmanacPanelSlot>();
            if (slot == null || slot.Entry == null) continue;

            total++;
            if (Mb_AlmanacManager.Instance.IsUnlocked(slot.Entry))
                unlocked++;
        }

        ProgressCounterText.text = $"{unlocked} / {total}";
    }

    #endregion                          //----------------------------------------


    #region Event Handlers              //----------------------------------------

    private void HandleEntryUnlocked(SO_WildlifeEntry entry)
    {
        if (_cardMap.TryGetValue(entry, out Mb_AlmanacDiamondCard card))
        {
            int completionCount = Mb_AlmanacManager.Instance.GetCompletionCount(entry);
            card.Initialize(entry, isUnlocked: true, completionCount, OnCardClicked);
        }

        RefreshProgressCounter();

        if (_selectedEntry == entry)
            PopulateDetailPanel(entry, isUnlocked: true);
    }


    private void HandleRepeatCompleted(SO_WildlifeEntry entry)
    {
        if (_cardMap.TryGetValue(entry, out Mb_AlmanacDiamondCard card))
        {
            int completionCount = Mb_AlmanacManager.Instance.GetCompletionCount(entry);
            card.Initialize(entry, isUnlocked: true, completionCount, OnCardClicked);
        }
    }

    #endregion                          //----------------------------------------


    #region Navigation                  //----------------------------------------

    public void OnCloseClicked()
    {
        MainMenuController?.ShowMainMenu();
    }

    #endregion                          //----------------------------------------


    #region String Builders             //----------------------------------------

    private string BuildStatBonusString(SO_WildlifeEntry entry)
    {
        if (entry.StatBonus == null) return "???";

        string statName = entry.StatBonus.TargetStat switch
        {
            StatType.MaxHealth => "Max HP",
            StatType.HealthRegen => "HP Regen",
            StatType.MoveSpeed => "Move Speed",
            StatType.AttackSpeed => "Attack Speed",
            StatType.AttackPower => "Attack Power",
            StatType.AbilityPower => "Ability Power",
            StatType.Haste => "Haste",
            StatType.CriticalChance => "Critical Chance",
            StatType.CriticalDamage => "Critical Damage",
            StatType.Lifesteal => "Lifesteal",
            StatType.Shielding => "Shielding",
            _ => entry.StatBonus.TargetStat.ToString()
        };

        string valueStr = entry.StatBonus.Type == StatModType.Percent
            ? $"+{entry.StatBonus.Value * 100f:F0}%"
            : $"+{entry.StatBonus.Value}";

        return $"{valueStr} {statName}";
    }


    private string BuildStatusString(ConservationStatus status)
    {
        return status switch
        {
            ConservationStatus.CriticallyEndangered => "Critically Endangered (CR)",
            ConservationStatus.Endangered => "Endangered (EN)",
            ConservationStatus.NearThreatened => "Near Threatened (NT)",
            _ => status.ToString()
        };
    }

    #endregion                          //----------------------------------------
}