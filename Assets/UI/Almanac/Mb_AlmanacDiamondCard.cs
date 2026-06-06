// ─────────────────────────────────────────────────────────────────────────────
// Mb_AlmanacDiamondCard
// Lives on the diamond card prefab root.
// Owns all child UI references for one entry card in the grid.
// Kept in the same file since it only exists to serve Mb_AlmanacUI.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_AlmanacDiamondCard : MonoBehaviour
{

    #region Inspector Fields            //----------------------------------------

    [Header("Card UI References")]
    [Tooltip("Background Image of the diamond card. " +
             "Can tint differently for locked vs unlocked states.")]
    [SerializeField] private Image CardBackground;

    [Tooltip("Species icon — silhouette when locked, full icon when unlocked.")]
    [SerializeField] private Image IconImage;

    [Tooltip("'?' text shown when the entry is locked. Hidden when unlocked.")]
    [SerializeField] private GameObject LockedQuestionMark;

    [Tooltip("Small badge showing completion count (e.g. 'x2') for repeat completions. " +
             "Hidden when completion count is 1 (first unlock only).")]
    [SerializeField] private TMP_Text CompletionCountBadge;

    [Tooltip("The Button component on this card — OnClick wired in Initialize().")]
    [SerializeField] private Button CardButton;

    [Header("Card Colors")]
    [Tooltip("Background tint when the entry is locked.")]
    [SerializeField] private Color LockedColor = new Color(0.7f, 0.65f, 0.55f, 1f);

    [Tooltip("Background tint when the entry is unlocked.")]
    [SerializeField] private Color UnlockedColor = new Color(0.9f, 0.85f, 0.7f, 1f);

    #endregion                          //----------------------------------------


    #region Card State API              //----------------------------------------

    /// <summary>
    /// Sets up the card for a given entry and unlock state.
    /// Called by Mb_AlmanacUI.BuildGrid() and on unlock events.
    /// onClicked is called with the entry when the card is clicked.
    /// </summary>
    public void Initialize(
        SO_WildlifeEntry entry,
        bool isUnlocked,
        int completionCount,
        System.Action<SO_WildlifeEntry> onClicked)
    {
        // Wire the button callback — remove old listeners first to prevent stacking
        if (CardButton != null)
        {
            CardButton.onClick.RemoveAllListeners();
            CardButton.onClick.AddListener(() => onClicked?.Invoke(entry));
        }

        if (isUnlocked)
            SetUnlockedVisuals(entry, completionCount);
        else
            SetLockedVisuals(entry);
    }


    private void SetLockedVisuals(SO_WildlifeEntry entry)
    {
        // Show the silhouette icon if available, otherwise hide the icon entirely
        if (IconImage != null)
        {
            if (entry.SilhouetteIcon != null)
            {
                IconImage.sprite = entry.SilhouetteIcon;
                IconImage.gameObject.SetActive(true);
            }
            else
            {
                IconImage.gameObject.SetActive(false);
            }
        }

        // Show the question mark overlay on locked cards
        LockedQuestionMark?.SetActive(true);

        // Tint the background to locked color
        if (CardBackground != null)
            CardBackground.color = LockedColor;

        // Hide completion badge — irrelevant for locked entries
        if (CompletionCountBadge != null)
            CompletionCountBadge.gameObject.SetActive(false);
    }


    private void SetUnlockedVisuals(SO_WildlifeEntry entry, int completionCount)
    {
        if (IconImage != null)
        {
            IconImage.sprite = entry.UnlockedIcon != null
                ? entry.UnlockedIcon
                : entry.SilhouetteIcon; // Fallback until art is assigned

            IconImage.gameObject.SetActive(true);
        }

        // Hide question mark — entry is known
        LockedQuestionMark?.SetActive(false);

        // Tint to unlocked color
        if (CardBackground != null)
            CardBackground.color = UnlockedColor;

        // Show completion count badge only for repeat completions (count > 1)
        if (CompletionCountBadge != null)
        {
            if (completionCount > 1)
            {
                CompletionCountBadge.text = $"x{completionCount}";
                CompletionCountBadge.gameObject.SetActive(true);
            }
            else
            {
                CompletionCountBadge.gameObject.SetActive(false);
            }
        }
    }

    #endregion                          //----------------------------------------
}