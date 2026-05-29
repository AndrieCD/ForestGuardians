// Mb_VictoryScreenUI.cs
// The full-screen victory overlay — fades in after the victory sequence completes,
// displays the victory title, a contextual message, and navigation buttons.
//
// Inherits Mb_EndScreenUI so Mb_VictoryManager can hold a typed reference that plugs
// directly into Sc_EndSequenceConfig.Screen without any casting.
//
// CANVAS LAYOUT — set this up in the Unity Editor:
//   Victory Screen Canvas  ← this GameObject; starts INACTIVE (SetActive false)
//   ├── Overlay Panel      ← full-screen Image (gold/warm tint recommended); attach OverlayCanvasGroup
//   ├── Victory Title      ← TMP_Text; set to "VICTORY" in the Inspector
//   ├── Message Text       ← TMP_Text; left blank — Show() populates it
//   ├── Continue Button    ← Button (optional); wire OnClick → OnContinueClicked()
//   └── Main Menu Button   ← Button; wire OnClick → OnMainMenuClicked()
//
// DESIGNER NOTE ON OVERLAY COLOR:
//   Unlike the defeat screen (which uses a black overlay for a somber tone),
//   the victory overlay uses a warm gold/amber color to signal triumph.
//   Set the Overlay Panel's Image color to something like (255, 200, 80, 255)
//   and let it fade in from alpha 0 — the warm light "washing over" the scene
//   reinforces the emotional beat of winning.
//
// INSPECTOR SETUP:
//   - OverlayCanvasGroup: CanvasGroup on the full-screen tinted overlay Panel.
//   - VictoryTitleText: set to "VICTORY" in the Inspector — never changed at runtime.
//   - VictoryMessageText: leave blank — Show() populates it.
//   - MainMenuButton: wire OnClick to OnMainMenuClicked().
//   - ContinueButton: optional — wire OnClick to OnContinueClicked(). Used for
//     multi-stage progression. If unused, leave unassigned (null is guarded).
//   - FadeDuration: seconds for the overlay fade-in (default 1.5f — slightly longer
//     than defeat for a more triumphant, lingering feel).
//   - MainMenuSceneName: must match your main menu scene name in Build Settings.
//     TODO: Update "MainMenu" to match your actual scene name.
//   - NextStageName: the scene to load when Continue is clicked. Leave blank if
//     this stage is the final one — the Continue button will be hidden automatically.
//     TODO: Populate once Stage 2 scene exists.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Mb_VictoryScreenUI : Mb_EndScreenUI
{

    #region Inspector Fields            //----------------------------------------

    [Header("UI Elements")]
    [Tooltip("CanvasGroup on the full-screen tinted overlay Panel. Controls fade alpha.")]
    [SerializeField] private CanvasGroup _OverlayCanvasGroup;

    [Tooltip("TMP_Text for the victory title. Set to 'VICTORY' in the Inspector.")]
    [SerializeField] private TMP_Text _VictoryTitleText;

    [Tooltip("TMP_Text for the contextual victory message. Populated at runtime by Show().")]
    [SerializeField] private TMP_Text _VictoryMessageText;

    [Tooltip("Optional button to proceed to the next stage. " +
             "Hide this button in the Inspector if this is the final stage.")]
    [SerializeField] private Button _ContinueButton;

    [Tooltip("Button to return to the main menu.")]
    [SerializeField] private Button _MainMenuButton;

    [Header("Settings")]
    [Tooltip("Seconds for the overlay to fade from transparent to fully opaque. " +
             "Slightly longer than defeat (default 1.5f) for a more triumphant feel.")]
    [SerializeField] private float _FadeDuration = 1.5f;

    [Tooltip("Main menu scene name. Must match an entry in File > Build Settings.")]
    [SerializeField] private string _MainMenuSceneName = "MainMenu";
    // TODO: Update "MainMenu" to match your actual scene name in Build Settings.

    [Tooltip("Scene name for the next stage. Leave blank if this is the final stage — " +
             "the Continue button will be hidden automatically.")]
    [SerializeField] private string _NextStageName = "";
    // TODO: Populate once Stage 2's scene name is finalized.

    #endregion                          //----------------------------------------


    #region Mb_EndScreenUI              //----------------------------------------

    /// <summary>
    /// Called by Mb_VictoryManager after the victory sequence finishes.
    /// Activates the Canvas and fades in the overlay, then reveals the message.
    /// </summary>
    public override void Show(string message)
    {
        gameObject.SetActive(true);

        // Populate the message before the fade so it is ready the moment it becomes visible.
        if (_VictoryMessageText != null)
            _VictoryMessageText.text = message;

        if (_VictoryTitleText == null)
            Debug.LogWarning("[Mb_VictoryScreenUI] VictoryTitleText is not assigned in the Inspector.");

        if (_OverlayCanvasGroup == null)
        {
            Debug.LogError("[Mb_VictoryScreenUI] OverlayCanvasGroup is not assigned. " +
                           "Assign the CanvasGroup on the tinted overlay Panel.");
            return;
        }

        // Hide title and message until the overlay is fully opaque —
        // prevents text from appearing over the gameplay view mid-fade.
        SetTextVisibility(false);

        // Hide the Continue button if no next stage is configured.
        // This avoids showing a button that leads nowhere on the final stage.
        if (_ContinueButton != null)
            _ContinueButton.gameObject.SetActive(!string.IsNullOrEmpty(_NextStageName));

        StartCoroutine(FadeInRoutine());
    }

    #endregion                          //----------------------------------------


    #region Fade Coroutine              //----------------------------------------

    private IEnumerator FadeInRoutine()
    {
        _OverlayCanvasGroup.alpha = 0f;

        float elapsed = 0f;

        // Fade from transparent to opaque over FadeDuration seconds.
        // Time.unscaledDeltaTime is used as a safety net — Mb_VictoryManager's
        // config uses TimeScaleMultiplier = 1f (no time adjustment), so delta time
        // is normal. Unscaled delta guards against any edge case where timeScale
        // is non-standard at the moment Show() is called.
        while (elapsed < _FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _OverlayCanvasGroup.alpha = Mathf.Clamp01(elapsed / _FadeDuration);
            yield return null;
        }

        _OverlayCanvasGroup.alpha = 1f;

        // Overlay is fully opaque — safe to reveal title, message, and buttons.
        SetTextVisibility(true);
    }


    private void SetTextVisibility(bool visible)
    {
        if (_VictoryTitleText != null)
            _VictoryTitleText.gameObject.SetActive(visible);

        if (_VictoryMessageText != null)
            _VictoryMessageText.gameObject.SetActive(visible);
    }

    #endregion                          //----------------------------------------


    #region Button Handlers             //----------------------------------------

    /// <summary>
    /// Called by the Continue button's OnClick event.
    /// Loads the next stage scene. Only shown when NextStageName is populated.
    /// </summary>
    public void OnContinueClicked()
    {
        // Safety reset — ensures timeScale is normal before any scene transition.
        Time.timeScale = 1f;

        SceneManager.LoadScene(_NextStageName);
        GameManager.Instance.ChangeState(GameState.Playing);
    }


    /// <summary>
    /// Called by the Main Menu button's OnClick event.
    /// </summary>
    public void OnMainMenuClicked()
    {
        // Safety reset — same rationale as Mb_DefeatScreenUI.
        Time.timeScale = 1f;

        SceneManager.LoadScene(_MainMenuSceneName);
        GameManager.Instance.ChangeState(GameState.MainMenu);
    }

    #endregion                          //----------------------------------------
}