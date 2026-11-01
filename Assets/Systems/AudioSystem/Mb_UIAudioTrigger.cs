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
using UnityEngine.EventSystems;

public class Mb_UIAudioTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Button Sounds")]
    [SerializeField] private UISFX _OnClickSFX = UISFX.UI_Click_Generic;
    [SerializeField] private UISFX _OnHoverSFX = UISFX.UI_Hover;

    [Header("Panel Sounds")]
    [SerializeField] private bool _HasOpenSound = false;
    [SerializeField] private UISFX _OnOpenSFX = UISFX.UI_RewardPanel_Open;

    // Existing prefabs may still contain an EventTrigger PointerEnter entry.
    // The small guard prevents that legacy entry and the interface callback
    // from playing the hover cue twice during the same pointer transition.
    private float _lastHoverTime = -Mathf.Infinity;
    private float _lastClickTime = -Mathf.Infinity;


    private void Awake()
    {
        NormalizeLegacyEnumValues();
    }


    private void NormalizeLegacyEnumValues()
    {
        // Older prefab wiring stored enum list indices instead of the actual
        // UISFX values. Keep those prefabs working without requiring a manual
        // Inspector pass across every Application GUI asset.
        if ((int)_OnClickSFX == 0)
            _OnClickSFX = UISFX.UI_Click_Generic;

        if ((int)_OnHoverSFX == 3)
            _OnHoverSFX = UISFX.UI_Hover;

        if ((int)_OnOpenSFX == 6)
            _OnOpenSFX = UISFX.UI_RewardPanel_Open;
    }


    // Wire to Button.OnClick() in the Inspector
    public void OnClick()
    {
        if (Time.unscaledTime - _lastClickTime < 0.05f) return;

        _lastClickTime = Time.unscaledTime;
        Mb_AudioManager.PlayUI(_OnClickSFX);
    }

    /// <summary>
    /// Provides click audio even when a prefab has lost its Button.OnClick
    /// audio entry. Existing Inspector-wired OnClick callbacks remain supported.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick();
    }

    // Wire to EventTrigger PointerEnter in the Inspector
    public void OnHover()
    {
        if (Time.unscaledTime - _lastHoverTime < 0.05f) return;

        _lastHoverTime = Time.unscaledTime;
        Mb_AudioManager.PlayUI(_OnHoverSFX);
    }

    /// <summary>
    /// Provides hover audio even when a prefab has lost its EventTrigger entry.
    /// Existing Inspector-wired PointerEnter callbacks remain supported.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        OnHover();
    }

    /// <summary>
    /// Applies the common Application GUI sound policy at runtime:
    /// generic click and standard hover for every interactable.
    /// </summary>
    public void ConfigureApplicationMenuDefaults()
    {
        _OnClickSFX = UISFX.UI_Click_Generic;
        _OnHoverSFX = UISFX.UI_Hover;
    }

    // Call from whatever script shows this panel (e.g. Mb_RewardsPanelUI.Show())
    public void OnOpen()
    {
        if (!_HasOpenSound) return;
        Mb_AudioManager.PlayUI(_OnOpenSFX);
    }
}
