

using TMPro;
using UnityEngine;
using UnityEngine.UI;


// ─────────────────────────────────────────────────────────────────────────────
// Mb_AbilitySlotUI
// Lives on each ability icon slot in the grid.
// Owns the slot's Image and Button — Mb_GuardianSelectionUI drives it
// from the outside via SetIcon() and SetClickCallback().
// Kept in the same file since it only exists to serve Mb_GuardianSelectionUI.
// ─────────────────────────────────────────────────────────────────────────────

public class Mb_SelectionAbilitySlotUI : MonoBehaviour
{
    [Tooltip("The Image component that displays the ability icon.")]
    [SerializeField] private Image SlotIcon;

    [Tooltip("The Button component for click detection.")]
    [SerializeField] private Button SlotButton;

    //[Tooltip("Label below or beside the icon — e.g. 'Passive', 'Q', 'R1'. " +
    //         "Set the text directly in the Inspector on each slot prefab instance. " +
    //         "This label never changes at runtime.")]
    //[SerializeField] private TMP_Text SlotLabel;


    private void Awake()
    {
        if (SlotIcon == null)
            Debug.LogError($"[Mb_AbilitySlotUI] SlotIcon not assigned on {gameObject.name}.");

        if (SlotButton == null)
            Debug.LogError($"[Mb_AbilitySlotUI] SlotButton not assigned on {gameObject.name}.");
    }


    /// <summary>
    /// Sets the slot's icon sprite and color tint.
    /// Called by Mb_GuardianSelectionUI.RefreshAbilitySlotIcons() after any
    /// guardian or ability selection change.
    /// </summary>
    public void SetIcon(Sprite icon, Color tint)
    {
        if (SlotIcon == null) return;

        bool hasIcon = icon != null;
        SlotIcon.sprite = hasIcon ? icon : null;
        SlotIcon.color = tint;

        // Hide the slot entirely if no icon is assigned yet —
        // avoids a white square placeholder during early development
        SlotIcon.gameObject.SetActive(hasIcon);
    }


    /// <summary>
    /// Wires the click callback for this slot.
    /// Called by Mb_GuardianSelectionUI.OnEnable() so the callback is
    /// always fresh and never stacks across screen opens.
    /// </summary>
    public void SetClickCallback(UnityEngine.Events.UnityAction callback)
    {
        if (SlotButton == null) return;

        SlotButton.onClick.RemoveAllListeners();
        SlotButton.onClick.AddListener(callback);
    }
}