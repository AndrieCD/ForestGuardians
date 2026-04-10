// Mb_RewardsPanelUI.cs
// Drives the Rewards Panel Canvas — the UI the player sees when choosing a reward.
//
// LAYOUT (set up in the Inspector / Unity Editor):
//   Rewards Panel (Canvas root — this GameObject)
//   ├── Left Card (Button)
//   │   ├── Icon (Image)
//   │   ├── Name (TMP_Text)
//   │   └── Description (TMP_Text)
//   └── Right Card (Button)
//       ├── Icon (Image)
//       ├── Name (TMP_Text)
//       └── Description (TMP_Text)
//
// Inspector setup:
//   - Assign Left/Right card Image, Name Text, and Description Text fields.
//   - Wire Left Card Button's OnClick → Mb_RewardsPanelUI.OnLeftCardClicked
//   - Wire Right Card Button's OnClick → Mb_RewardsPanelUI.OnRightCardClicked
//   - Assign the Mb_RewardsManager reference (sibling on Stage GO, drag in).

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_RewardsPanelUI : MonoBehaviour
{
    [Header("Left Card")]
    [SerializeField] private Image _LeftIcon;
    [SerializeField] private TMP_Text _LeftName;
    [SerializeField] private TMP_Text _LeftDescription;

    [Header("Right Card")]
    [SerializeField] private Image _RightIcon;
    [SerializeField] private TMP_Text _RightName;
    [SerializeField] private TMP_Text _RightDescription;

    [Header("References")]
    // Drag Mb_RewardsManager here — it lives on the Stage GameObject
    [SerializeField] private Mb_RewardsManager _RewardsManager;

    // The root Canvas or panel — this is what we show/hide
    // (this component itself sits on the root, so we toggle its GameObject)


    /// <summary>
    /// Called by Mb_RewardsManager to populate and show the panel.
    /// </summary>
    public void Show(RewardOption left, RewardOption right)
    {
        PopulateCard(_LeftIcon, _LeftName, _LeftDescription, left);
        PopulateCard(_RightIcon, _RightName, _RightDescription, right);
        gameObject.SetActive(true);
    }


    /// <summary>
    /// Called by Mb_RewardsManager after the player makes a choice.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }


    // Button OnClick callbacks — wire these in the Inspector on each Card Button

    public void OnLeftCardClicked()
    {
        // Disable both cards immediately so the player can't double-click
        SetCardsInteractable(false);
        _RewardsManager.OnLeftChosen();
    }

    public void OnRightCardClicked()
    {
        SetCardsInteractable(false);
        _RewardsManager.OnRightChosen();
    }


    private void PopulateCard(Image iconImg, TMP_Text nameText, TMP_Text descText, RewardOption option)
    {
        nameText.text = option.Name;
        descText.text = option.Description;

        // Only show the icon image if the SO provided one
        if (option.Icon != null)
        {
            iconImg.sprite = option.Icon;
            iconImg.gameObject.SetActive(true);
        }
        else
        {
            iconImg.gameObject.SetActive(false);
        }
    }


    private void SetCardsInteractable(bool state)
    {
        // Re-enable when Show() is called next time — this just prevents
        // the player clicking twice before the panel closes
        var buttons = GetComponentsInChildren<Button>();
        foreach (var btn in buttons)
            btn.interactable = state;
    }


    private void OnEnable()
    {
        // Re-enable card buttons every time the panel opens fresh
        SetCardsInteractable(true);
    }
}