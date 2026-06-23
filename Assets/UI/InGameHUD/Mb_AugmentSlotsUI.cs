// Mb_AugmentSlotsUI.cs
// Displays the player's currently equipped augments as icons on the HUD.
// Attach this to the augment slots container on the healthbar.
//
// HOW IT WORKS:
//   - Subscribes to Mb_WaveManager.OnWaveEnd — augments are only ever
//     added between waves via the rewards panel, so this is the correct
//     moment to refresh the display.
//   - Also refreshes on Mb_RewardsManager.OnRewardChosen so the icon
//     appears immediately when the player picks an augment, before
//     the rewards panel closes.
//   - Reads the equipped augment list from Mb_AugmentManager and assigns
//     sprites to up to 3 pre-built slot Image components.
//
// Inspector Setup:
//   - AugmentManager: drag the Stage GameObject that has Mb_AugmentManager.
//   - Slots: assign exactly 3 Image components (the cyan circle slots).
//     They should already exist in Leo's healthbar layout.
//   - EmptySlotSprite: optional sprite shown when a slot has no augment yet.
//     Leave null to hide empty slots entirely.

using UnityEngine;
using UnityEngine.UI;

public class Mb_AugmentSlotsUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Stage GameObject that has Mb_AugmentManager on it.")]
    [SerializeField] private Mb_AugmentManager AugmentManager;

    [Header("Slots")]
    [Tooltip("Assign the 3 augment slot Image components in order (top to bottom or as laid out).")]
    [SerializeField] private Image[] Slots = new Image[3];

    [Header("Visuals")]
    [Tooltip("Sprite shown on empty slots. Leave null to hide empty slots instead.")]
    [SerializeField] private Sprite EmptySlotSprite;

    // TODO: Tune slot alpha for empty state — 0.3 reads as inactive without disappearing.
    [SerializeField][Range(0f, 1f)] private float EmptySlotAlpha = 0.3f;
    [SerializeField][Range(0f, 1f)] private float FilledSlotAlpha = 1.0f;


    private void OnEnable()
    {
        Mb_AugmentManager.OnAugmentsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Mb_AugmentManager.OnAugmentsChanged -= Refresh;
    }


    private void HandleRewardChosen(RewardOption option)
    {
        // Only refresh if an augment was picked — level ups and branch
        // selections don't change the augment slot display
        //if (option.Type == RewardType.AugmentSelection)
            Refresh();
    }


    private void Refresh()
    {
        if (AugmentManager == null)
        {
            Debug.LogError("[Mb_AugmentSlotsUI] AugmentManager reference is not assigned.");
            return;
        }

        var equipped = AugmentManager.GetEquippedAugments();

        for (int i = 0; i < Slots.Length; i++)
        {
            Debug.Log($"[Mb_AugmentSlotsUI] Refreshing slot {i}: {(i < equipped.Count ? equipped[i].AugmentName : "Empty")}");

            if (Slots[i] == null) continue;

            Debug.Log($"[Mb_AugmentSlotsUI] Slot {i} Image component: {Slots[i].name}");

            if (i < equipped.Count && equipped[i].Icon != null)
            {
                // Slot is filled — show the augment icon at full opacity
                Slots[i].sprite = equipped[i].Icon;
                Slots[i].enabled = true;
                SetAlpha(Slots[i], FilledSlotAlpha);
            }
            else
            {
                // Slot is empty — show placeholder or hide
                if (EmptySlotSprite != null)
                {
                    Slots[i].sprite = EmptySlotSprite;
                    Slots[i].enabled = true;
                    SetAlpha(Slots[i], EmptySlotAlpha);
                }
                else
                {
                    // No placeholder assigned — hide the slot entirely
                    Slots[i].enabled = false;
                }
            }
        }
    }


    private void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}