// Mb_UIAudioTrigger.cs
// Drop this on any UI GameObject (Button, Panel, etc.) to give it audio.
//
// HOW TO USE:
//   1. Add this component to a Button or panel GameObject in the Inspector.
//   2. Set OnClickSFX and/or OnHoverSFX to the enum value you want.
//   3. For Buttons: wire the Button's OnClick() → Mb_UIAudioTrigger.OnClick()
//      and EventTrigger's PointerEnter → Mb_UIAudioTrigger.OnHover()
//   4. For panels opening/closing: call OnOpen() / OnClose() from the script
//      that shows/hides the panel — e.g. Mb_RewardsPanelUI.Show() calls
//      GetComponent<Mb_UIAudioTrigger>().OnOpen()
//
// INSPECTOR SETUP:
//   - OnClickSFX:  sound when this button is clicked (default: UI_Click_Generic)
//   - OnHoverSFX:  sound when mouse enters this button (default: UI_Hover)
//   - OnOpenSFX:   sound when this panel becomes visible (leave at None to skip)
//   - HasOpenSound: toggle — only panels need an open sound, not every button

using UnityEngine;

public class Mb_UIAudioTrigger : MonoBehaviour
{
    [Header("Button Sounds")]
    [SerializeField] private UISFX _OnClickSFX = UISFX.UI_Click_Generic;
    [SerializeField] private UISFX _OnHoverSFX = UISFX.UI_Hover;

    [Header("Panel Sounds")]
    [SerializeField] private bool _HasOpenSound = false;
    [SerializeField] private UISFX _OnOpenSFX = UISFX.UI_RewardPanel_Open;


    // Wire to Button.OnClick() in the Inspector
    public void OnClick()
    {
        Mb_AudioManager.PlayUI(_OnClickSFX);
    }

    // Wire to EventTrigger PointerEnter in the Inspector
    public void OnHover()
    {
        Mb_AudioManager.PlayUI(_OnHoverSFX);
    }

    // Call from whatever script shows this panel (e.g. Mb_RewardsPanelUI.Show())
    public void OnOpen()
    {
        if (!_HasOpenSound) return;
        Mb_AudioManager.PlayUI(_OnOpenSFX);
    }
}