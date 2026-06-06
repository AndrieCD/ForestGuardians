// Mb_DialogUI.cs
// The focused UI component that owns and drives the dialog panel's visual state.
// Mb_DialogManager calls into this — it never touches UI elements directly.
//
// LAYOUT (set up in the Inspector / Unity Editor):
//   DialogPanel (this GameObject — Mb_DialogUI lives here)
//   ├── PortraitImage       (Image — hidden when SO_Dialog.Portrait is null)
//   ├── SpeakerNameText     (TMP_Text — hidden when SO_Dialog.SpeakerName is empty)
//   ├── DialogText          (TMP_Text — full text, no typewriter effect)
//   └── ContinueIndicator   (GameObject — small arrow/icon shown only when
//                            IsTutorialInstruction is true AND audio has finished,
//                            signalling the player must complete a task to proceed)
//
// INSPECTOR SETUP:
//   - Assign all four references in the Inspector.
//   - ContinueIndicator: any GameObject (Image, animated arrow, etc.) — this script
//     only toggles its active state.
//   - This component's GameObject IS the panel — Show() activates it, Hide() deactivates it.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_DialogUI : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Header("UI References")]
    [Tooltip("Image component for the NPC portrait. Hidden when Portrait sprite is null.")]
    [SerializeField] private Image _PortraitImage;

    [Tooltip("TMP_Text for the speaker's name. Hidden when SpeakerName is empty.")]
    [SerializeField] private TMP_Text _SpeakerNameText;

    [Tooltip("TMP_Text for the full dialog line.")]
    [SerializeField] private TMP_Text _DialogText;

    [Tooltip("GameObject shown when a tutorial instruction is waiting for player action. " +
             "Hidden at all other times.")]
    [SerializeField] private GameObject _ContinueIndicator;

    #endregion                  //----------------------------------------


    #region Public API          //----------------------------------------

    /// <summary>
    /// Populates all UI fields from the given SO_Dialog and activates the panel.
    /// ContinueIndicator is always hidden on show — Mb_DialogManager enables it
    /// later via SetContinueIndicatorVisible() once audio finishes on a tutorial dialog.
    /// </summary>
    public void Show(SO_Dialog dialog)
    {
        if (dialog == null)
        {
            Debug.LogError("[Mb_DialogUI] Show() called with null SO_Dialog.");
            return;
        }

        // Portrait — hide the image element entirely when no sprite is provided
        if (_PortraitImage != null)
        {
            bool hasPortrait = dialog.Portrait != null;
            _PortraitImage.gameObject.SetActive(hasPortrait);

            if (hasPortrait)
                _PortraitImage.sprite = dialog.Portrait;
        }

        // Speaker name — hide the label entirely when the name is blank
        if (_SpeakerNameText != null)
        {
            bool hasName = !string.IsNullOrEmpty(dialog.SpeakerName);
            _SpeakerNameText.gameObject.SetActive(hasName);

            if (hasName)
                _SpeakerNameText.text = dialog.SpeakerName;
        }

        // Dialog text — always shown
        if (_DialogText != null)
            _DialogText.text = dialog.DialogText;

        // ContinueIndicator always starts hidden — enabled later if this is a
        // tutorial instruction and audio has finished
        SetContinueIndicatorVisible(false);

        gameObject.SetActive(true);
    }


    /// <summary>
    /// Deactivates the panel and resets all fields to a clean state.
    /// Called by Mb_DialogManager when a dialog is dismissed.
    /// </summary>
    public void Hide()
    {
        SetContinueIndicatorVisible(false);

        if (_SpeakerNameText != null)
            _SpeakerNameText.text = string.Empty;

        if (_DialogText != null)
            _DialogText.text = string.Empty;

        if (_PortraitImage != null)
            _PortraitImage.sprite = null;

        gameObject.SetActive(false);
    }


    /// <summary>
    /// Shows or hides the ContinueIndicator.
    /// Called by Mb_DialogManager after audio ends on a tutorial instruction dialog —
    /// signals to the player that they must perform an action to proceed.
    /// </summary>
    public void SetContinueIndicatorVisible(bool visible)
    {
        if (_ContinueIndicator != null)
            _ContinueIndicator.SetActive(visible);
    }

    #endregion                  //----------------------------------------
}