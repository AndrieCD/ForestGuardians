using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// Mb_WildlifeHotbarSlot
// Lives on the slot prefab root. Owns all child UI references for one slot.
// Kept in the same file since it only exists to serve Mb_WildlifeHotbarUI.
// ─────────────────────────────────────────────────────────────────────────────

public class Mb_WildlifeHotbarSlot : MonoBehaviour
{

    #region Inspector Fields            //----------------------------------------

    [Header("Slot UI References")]
    [Tooltip("Displays the species icon — silhouette while locked, full icon when complete.")]
    [SerializeField] private Image Icon;

    [Tooltip("Displays '???' while locked, shows CommonName on completion.")]
    [SerializeField] private TMP_Text SpeciesNameText;

    [Tooltip("Displays found/required count e.g. '0 / 2'.")]
    [SerializeField] private TMP_Text CounterText;

    [Tooltip("GameObject shown only when the species is fully collected. " +
             "Can be a checkmark, highlight border, or completion glow.")]
    [SerializeField] private GameObject CompletionOverlay;

    #endregion                          //----------------------------------------


    #region Slot State API              //----------------------------------------

    /// <summary>
    /// Sets the slot to its initial locked state:
    ///   - Silhouette icon
    ///   - "???" species name
    ///   - "0 / N" counter
    ///   - Completion overlay hidden
    /// </summary>
    public void InitializeLocked(SO_WildlifeEntry entry)
    {
        // Show silhouette while the species is not yet unlocked
        if (Icon != null)
        {
            Icon.sprite = entry.SilhouetteIcon;
            Icon.gameObject.SetActive(entry.SilhouetteIcon != null);
        }

        if (SpeciesNameText != null)
            SpeciesNameText.text = "???";

        if (CounterText != null)
            CounterText.text = $"0 / {entry.RequiredCount}";

        if (CompletionOverlay != null)
            CompletionOverlay.SetActive(false);
    }


    /// <summary>
    /// Updates the found/required counter text.
    /// Called by Mb_WildlifeHotbarUI on OnSpeciesProgress.
    /// </summary>
    public void UpdateCounter(int found, int required)
    {
        if (CounterText != null)
            CounterText.text = $"{found} / {required}";
    }


    /// <summary>
    /// Transitions the slot to its completed state:
    ///   - Full species icon (unlocked)
    ///   - Shows CommonName
    ///   - Counter shows N / N
    ///   - Completion overlay shown
    /// </summary>
    public void MarkComplete(SO_WildlifeEntry entry)
    {
        if (Icon != null)
        {
            // Swap to the full unlocked icon on completion
            Icon.sprite = entry.UnlockedIcon != null
                ? entry.UnlockedIcon
                : entry.SilhouetteIcon; // Fallback if unlocked icon not yet assigned

            Icon.gameObject.SetActive(true);
        }

        if (SpeciesNameText != null)
            SpeciesNameText.text = entry.CommonName;

        if (CounterText != null)
            CounterText.text = $"{entry.RequiredCount} / {entry.RequiredCount}";

        if (CompletionOverlay != null)
            CompletionOverlay.SetActive(true);
    }

    #endregion                          //----------------------------------------
}