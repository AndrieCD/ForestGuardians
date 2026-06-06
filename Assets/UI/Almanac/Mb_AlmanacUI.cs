// Mb_AlmanacUI.cs
// Lives on the Almanac Canvas in the main menu scene.
// Drives the two-page book layout:
//   Left page  — diamond grid of all 13 entry cards + progress counter
//   Right page — detail panel for the selected entry
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
//     // TODO: Create a second Camera rendering to that RenderTexture, position
//     //       it to frame the ModelPrefab once 3D models are ready
//   - Wire CloseButton.OnClick → Mb_AlmanacUI.OnCloseClicked in Inspector
//   - This Canvas starts inactive — activated by MainMenuController.OnAlmanacClicked()

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_AlmanacUI : MonoBehaviour
{

    #region Inspector Fields            //----------------------------------------

    [Header("Left Page")]
    [Tooltip("Image at the top of the left page — title art or logo.")]
    [SerializeField] private Image HeaderImage;

    [Tooltip("Flavour text below the header. " +
             "Suggested: 'The missing creatures of Panohara need your help! " +
             "Set out in the wilds to find them and earn their blessings.'")]
    [SerializeField] private TMP_Text FlavourText;

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
    // TODO: Create a dedicated Camera in the scene rendering to a RenderTexture asset.
    //       Position it to frame the ModelPrefab once 3D models are available.
    //       Assign the same RenderTexture to both the Camera's Output Texture
    //       and this RawImage's Texture field.
    [SerializeField] private RawImage ModelViewerRawImage;

    [Tooltip("GameObject that holds the model in the viewer scene. " +
             "Swap its child mesh when a new entry is selected.")]
    // TODO: Assign once 3D model viewer camera rig is built in the scene.
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

    [Tooltip("References field at the bottom of the right page. " +
             "Populate manually per entry or leave as stub.")]
    // TODO: Add a References string field to SO_WildlifeEntry if per-species
    //       citation text is needed for the thesis documentation.
    [SerializeField] private TMP_Text ReferencesText;

    [Header("Right Page — Empty State")]
    [Tooltip("Shown on the right page when no card has been selected yet. " +
             "Hide this when a card is selected.")]
    [SerializeField] private GameObject EmptyDetailPanel;

    [Tooltip("Root that holds all detail fields. Hidden until a card is selected.")]
    [SerializeField] private GameObject DetailContentPanel;

    [Header("Navigation")]
    [Tooltip("The X close button on the right edge. " +
             "Wire its OnClick to Mb_AlmanacUI.OnCloseClicked in the Inspector.")]
    [SerializeField] private Button CloseButton;

    #endregion                          //----------------------------------------


    #region Runtime State               //----------------------------------------

    // All instantiated diamond card components — kept so we can refresh them
    // when OnEntryUnlocked fires mid-session
    private List<Mb_AlmanacDiamondCard> _cards = new List<Mb_AlmanacDiamondCard>();

    // Maps each entry to its card for O(1) targeted refresh
    private Dictionary<SO_WildlifeEntry, Mb_AlmanacDiamondCard> _cardMap
        = new Dictionary<SO_WildlifeEntry, Mb_AlmanacDiamondCard>();

    // The entry whose detail is currently shown on the right page
    private SO_WildlifeEntry _selectedEntry;

    #endregion                          //----------------------------------------


    #region Unity Lifecycle             //----------------------------------------

    private void OnEnable()
    {
        // Subscribe to unlock events so newly unlocked entries refresh their
        // cards and detail panel without needing a full rebuild
        Mb_AlmanacManager.OnEntryUnlocked += HandleEntryUnlocked;
        Mb_AlmanacManager.OnRepeatCompleted += HandleRepeatCompleted;

        // Rebuild the grid every time the Almanac is opened —
        // catches any unlocks that happened since last open
        BuildGrid();
        RefreshProgressCounter();
        ShowEmptyDetail();
    }

    private void OnDisable()
    {
        Mb_AlmanacManager.OnEntryUnlocked -= HandleEntryUnlocked;
        Mb_AlmanacManager.OnRepeatCompleted -= HandleRepeatCompleted;
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

        // Iterate every Panel child under DiamondGrid.
        // Each Panel that has an Mb_AlmanacPanelSlot with an Entry assigned
        // gets one card instantiated and parented to it.
        // Panels without a slot component or with a null Entry are skipped.
        foreach (Transform panel in DiamondGrid)
        {
            Mb_AlmanacPanelSlot slot = panel.GetComponent<Mb_AlmanacPanelSlot>();

            // Skip panels with no slot component or no entry assigned
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

            // Stretch the card to fill its parent Panel exactly —
            // the Panel's RectTransform controls position and size,
            // the card just fills it
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
        // Destroy only the instantiated card children inside each Panel —
        // never destroy the Panels themselves since they are manually
        // positioned by the designer and must persist
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

    /// <summary>
    /// Called by each Mb_AlmanacDiamondCard when clicked.
    /// Populates the right page detail panel with the entry's data.
    /// </summary>
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
        // Switch from empty state to content panel
        EmptyDetailPanel?.SetActive(false);
        DetailContentPanel?.SetActive(true);

        if (isUnlocked)
            PopulateUnlocked(entry);
        else
            PopulateLocked(entry);
    }


    /// <summary>
    /// Fills the right page with full species data for an unlocked entry.
    /// </summary>
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

        // Population and Habitat are filled directly from the SO —
        // these fields are set by the designer in the Inspector
        if (PopulationText != null)
            PopulationText.text = $"Population: {entry.Population}";

        if (HabitatText != null)
            HabitatText.text = $"Habitat: {entry.Habitat}";

        if (DescriptionText != null)
            DescriptionText.text = entry.Description;

        // TODO: Populate ReferencesText from SO_WildlifeEntry.References
        //       once that field is added to the SO
        if (ReferencesText != null)
            ReferencesText.text = "References:";
    }


    /// <summary>
    /// Fills the right page with ??? placeholders for a locked entry.
    /// Conservation status is intentionally shown — it hints at rarity
    /// and explains why certain species are harder to find in the wild.
    /// </summary>
    private void PopulateLocked(SO_WildlifeEntry entry)
    {
        if (NameBannerText != null)
            NameBannerText.text = "???";

        if (StatBonusText != null)
            StatBonusText.text = "???";

        if (ScientificNameText != null)
            ScientificNameText.text = "???";

        // Conservation status is always visible even on locked entries —
        // rarity hints help players understand spawn probability
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

    /// <summary>
    /// Swaps the model displayed in the 3D viewer panel.
    /// Instantiates the entry's ModelPrefab under ModelViewerRoot.
    /// </summary>
    private void SwapModelViewer(SO_WildlifeEntry entry, bool isUnlocked)
    {
        if (ModelViewerRoot == null) return;

        // Destroy the previously displayed model
        foreach (Transform child in ModelViewerRoot.transform)
            Destroy(child.gameObject);

        // Only show the model if the entry is unlocked and a prefab is assigned
        if (!isUnlocked || entry.ModelPrefab == null)
        {
            // TODO: Optionally show a silhouette mesh or "???" placeholder
            //       in the viewer when the entry is locked
            ModelViewerRawImage?.gameObject.SetActive(false);
            return;
        }

        ModelViewerRawImage?.gameObject.SetActive(true);

        // Instantiate the model under the viewer root —
        // the viewer Camera renders this root to its RenderTexture
        GameObject model = Instantiate(entry.ModelPrefab, ModelViewerRoot.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // TODO: Add a slow auto-rotation behaviour to the model for presentation polish
        // Suggested: model.AddComponent<Mb_ModelAutoRotate>() once that utility exists
    }

    #endregion                          //----------------------------------------


    #region Progress Counter            //----------------------------------------

    private void RefreshProgressCounter()
    {
        if (ProgressCounterText == null) return;
        if (Mb_AlmanacManager.Instance == null) return;

        // Count total assigned panels — not AllEntries.Count —
        // so the denominator matches exactly what's visible in the grid
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
        // Refresh the specific card that just unlocked
        if (_cardMap.TryGetValue(entry, out Mb_AlmanacDiamondCard card))
        {
            int completionCount = Mb_AlmanacManager.Instance.GetCompletionCount(entry);
            card.Initialize(entry, isUnlocked: true, completionCount, OnCardClicked);
        }

        // Refresh the progress counter
        RefreshProgressCounter();

        // If this entry is currently selected on the right page, refresh the detail
        if (_selectedEntry == entry)
            PopulateDetailPanel(entry, isUnlocked: true);
    }


    private void HandleRepeatCompleted(SO_WildlifeEntry entry)
    {
        // Refresh the card's completion count badge
        if (_cardMap.TryGetValue(entry, out Mb_AlmanacDiamondCard card))
        {
            int completionCount = Mb_AlmanacManager.Instance.GetCompletionCount(entry);
            card.Initialize(entry, isUnlocked: true, completionCount, OnCardClicked);
        }
    }

    #endregion                          //----------------------------------------


    #region Navigation                  //----------------------------------------

    /// <summary>
    /// Called by the Close (X) Button's OnClick in the Inspector.
    /// Hides the Almanac canvas and returns to the main menu state.
    /// </summary>
    public void OnCloseClicked()
    {
        gameObject.SetActive(false);

        // TODO: Fire GameManager.Instance.ChangeState(GameState.MainMenu) here
        //       once the main menu state fully drives canvas visibility via UIManager
    }

    #endregion                          //----------------------------------------


    #region String Builders             //----------------------------------------

    /// <summary>
    /// Builds a human-readable stat bonus string from the entry's StatBonus effect.
    /// e.g. "+20% Critical Damage" or "+200 Max HP"
    /// </summary>
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

        // Format value — percent modifiers display as % values, flat as raw numbers
        string valueStr = entry.StatBonus.Type == StatModType.Percent
            ? $"+{entry.StatBonus.Value * 100f:F0}%"
            : $"+{entry.StatBonus.Value:F0}";

        return $"{valueStr} {statName}";
    }


    /// <summary>
    /// Converts a ConservationStatus enum to a readable display string
    /// with an IUCN-style label.
    /// </summary>
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


