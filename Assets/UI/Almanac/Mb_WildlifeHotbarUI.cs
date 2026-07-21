// Mb_WildlifeHotbarUI.cs
// Lives on the in-stage HUD Canvas.
// Displays the active quest species as a row of slots during a stage run.
//
// LAYOUT (set up in the Inspector / Unity Editor):
//   Wildlife Hotbar (this GameObject)
//   └── Slot Prefab (instantiated once per quest species at stage start)
//       ├── Icon (Image)           — silhouette while locked, full icon when complete
//       ├── SpeciesName (TMP_Text) — "???" while locked, CommonName when complete
//       ├── CounterText (TMP_Text) — "0 / 2" style progress display
//       └── CompletionOverlay (GameObject) — checkmark or highlight, hidden until done
//
// USAGE FLOW:
//   1. OnStageStart fires → InitializeSlots() builds one slot per quest species
//   2. OnSpeciesProgress fires → UpdateSlot() refreshes counter text
//   3. OnSpeciesCompleted fires → MarkSlotComplete() swaps icon and shows overlay
//   4. OnStageEnd fires → hotbar hides itself cleanly
//
// Inspector setup:
//   - Assign SlotPrefab — a prefab matching the layout described above
//   - Assign SlotsContainer — the parent RectTransform slots are spawned under
//   - Hotbar root (this GameObject) starts inactive — activated on stage start
//     so it never flickers on the main menu or loading screen

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_WildlifeHotbarUI : MonoBehaviour
{

    #region Inspector Fields            //----------------------------------------

    [Header("Slot Setup")]
    [Tooltip("Prefab for one quest species slot. Must have Mb_WildlifeHotbarSlot on its root.")]
    [SerializeField] private GameObject SlotPrefab;

    [Tooltip("Parent RectTransform that slots are instantiated under. " +
             "Use a Horizontal Layout Group for automatic spacing.")]
    [SerializeField] private RectTransform SlotsContainer;

    #endregion                          //----------------------------------------


    #region Runtime State               //----------------------------------------

    // Maps each quest entry to its instantiated slot component
    // so progress updates can find the right slot in O(1)
    private Dictionary<SO_WildlifeEntry, Mb_WildlifeHotbarSlot> _slotMap
        = new Dictionary<SO_WildlifeEntry, Mb_WildlifeHotbarSlot>();

    #endregion                          //----------------------------------------


    #region Unity Lifecycle             //----------------------------------------

    private void OnEnable()
    {
        Mb_StageManager.OnStageStart += HandleStageStart;
        Mb_StageManager.OnStageEnd += HandleStageEnd;
        Mb_WildlifeDiscoveryManager.OnSpeciesProgress += HandleSpeciesProgress;
        Mb_WildlifeDiscoveryManager.OnSpeciesCompleted += HandleSpeciesCompleted;
    }

    private void OnDisable()
    {
        Mb_StageManager.OnStageStart -= HandleStageStart;
        Mb_StageManager.OnStageEnd -= HandleStageEnd;
        Mb_WildlifeDiscoveryManager.OnSpeciesProgress -= HandleSpeciesProgress;
        Mb_WildlifeDiscoveryManager.OnSpeciesCompleted -= HandleSpeciesCompleted;
    }

    private void Start()
    {
        //// Hotbar starts hidden — only shown during an active stage
        //gameObject.SetActive(false);
    }

    #endregion                          //----------------------------------------


    #region Event Handlers              //----------------------------------------

    private void HandleStageStart()
    {
        // Mb_WildlifeDiscoveryManager.SelectQuestSpecies() runs on the same
        // OnStageStart event. We need its quest list to be ready before we
        // build slots. A one-frame yield isn't reliable here — instead we
        // rely on script execution order:
        // Mb_WildlifeDiscoveryManager should execute BEFORE Mb_WildlifeHotbarUI.
        // TODO: Set script execution order in Project Settings > Script Execution Order:
        //       Mb_WildlifeDiscoveryManager = -10, Mb_WildlifeHotbarUI = 0

        if (Mb_WildlifeDiscoveryManager.instance == null)
        {
            Debug.LogWarning("[Mb_WildlifeHotbarUI] No Mb_WildlifeDiscoveryManager " +
                             "found in scene — hotbar will not initialize.");
            return;
        }

        InitializeSlots(Mb_WildlifeDiscoveryManager.instance.GetQuestSpecies());
        gameObject.SetActive(true);
    }


    private void HandleStageEnd()
    {
        gameObject.SetActive(false);
        ClearSlots();
    }


    private void HandleSpeciesProgress(SO_WildlifeEntry entry, int found, int required)
    {
        if (!_slotMap.TryGetValue(entry, out Mb_WildlifeHotbarSlot slot)) return;
        slot.UpdateCounter(found, required);
        slot.PlayDiscoveryFeedback();
    }


    private void HandleSpeciesCompleted(SO_WildlifeEntry entry)
    {
        if (!_slotMap.TryGetValue(entry, out Mb_WildlifeHotbarSlot slot)) return;
        slot.MarkComplete(entry);
        slot.PlayDiscoveryFeedback();
    }

    #endregion                          //----------------------------------------


    #region Slot Management             //----------------------------------------

    private void InitializeSlots(IReadOnlyList<SO_WildlifeEntry> questSpecies)
    {
        ClearSlots();

        if (SlotPrefab == null)
        {
            Debug.LogError("[Mb_WildlifeHotbarUI] SlotPrefab is not assigned.");
            return;
        }

        if (SlotsContainer == null)
        {
            Debug.LogError("[Mb_WildlifeHotbarUI] SlotsContainer is not assigned.");
            return;
        }

        foreach (SO_WildlifeEntry entry in questSpecies)
        {
            // Instantiate one slot per quest species under the container
            GameObject slotGO = Instantiate(SlotPrefab, SlotsContainer);
            Mb_WildlifeHotbarSlot slot = slotGO.GetComponent<Mb_WildlifeHotbarSlot>();

            if (slot == null)
            {
                Debug.LogError("[Mb_WildlifeHotbarUI] SlotPrefab is missing " +
                               "Mb_WildlifeHotbarSlot component.");
                continue;
            }

            // Initialize the slot in its locked state —
            // species name hidden, silhouette shown, counter at 0
            slot.InitializeLocked(entry);
            _slotMap[entry] = slot;
        }
    }


    /// <summary>
    /// Destroys all instantiated slot GameObjects and clears the map.
    /// Called on stage end and before re-initializing on a new stage start.
    /// </summary>
    private void ClearSlots()
    {
        // Destroy all child GameObjects under the container
        if (SlotsContainer != null)
        {
            foreach (Transform child in SlotsContainer)
                Destroy(child.gameObject);
        }

        _slotMap.Clear();
    }

    #endregion                          //----------------------------------------
}


