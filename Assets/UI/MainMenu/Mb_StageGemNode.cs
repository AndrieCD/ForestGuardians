using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component that lives on each stage gem Button GameObject.
/// Owns the gem's Image reference and exposes SetVisualState()
/// so Mb_StageSelectionUI can update visuals without knowing the
/// internal structure of each gem prefab.
/// </summary>
public class Mb_StageGemNode : MonoBehaviour
{
    [Tooltip("The Image component that represents this gem visually. " +
             "Color is tinted based on unlock and selection state.")]
    [SerializeField] private Image GemImage;

    [Tooltip("The Button component on this gem. " +
             "Set non-interactable for locked gems.")]
    [SerializeField] private Button GemButton;

    [Tooltip("Optional GameObject shown only when this gem is selected — " +
             "e.g. a highlight ring or glow overlay. " +
             "Leave unassigned if selection is communicated by color alone.")]
    [SerializeField] private GameObject SelectionIndicator;


    private void Awake()
    {
        if (GemImage == null)
            Debug.LogError($"[Mb_StageGemNode] GemImage not assigned on {gameObject.name}.");

        if (GemButton == null)
            Debug.LogError($"[Mb_StageGemNode] GemButton not assigned on {gameObject.name}.");
    }


    /// <summary>
    /// Wires the click callback for this gem.
    /// Called by Mb_StageSelectionUI.OnEnable() so the callback is always fresh.
    /// </summary>
    public void SetClickCallback(UnityEngine.Events.UnityAction callback)
    {
        if (GemButton == null) return;

        GemButton.onClick.RemoveAllListeners();
        GemButton.onClick.AddListener(callback);
    }


    /// <summary>
    /// Updates the gem's visual state based on unlock and selection status.
    /// Called by Mb_StageSelectionUI.RefreshGems() every time state changes.
    /// </summary>
    public void SetVisualState(bool isUnlocked, bool isSelected, Color gemColor)
    {
        // Tint the gem image to communicate its state
        if (GemImage != null)
            GemImage.color = gemColor;

        // Locked gems are not interactable — clicking them calls OnGemClicked
        // which shows the locked detail panel, but the button itself still
        // needs to be interactable for that click to register.
        // We keep interactable = true always and let OnGemClicked handle
        // the locked state, so the player gets feedback instead of silence.
        if (GemButton != null)
            GemButton.interactable = true;

        // Show or hide the optional selection indicator overlay
        if (SelectionIndicator != null)
            SelectionIndicator.SetActive(isSelected);
    }
}