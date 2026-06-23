// Mb_DefeatScreenUI.cs
// The full-screen defeat overlay — fades in after the defeat sequence completes,
// displays a contextual message, and offers a Main Menu button.
//
// Inherits Mb_EndScreenUI so Mb_DefeatManager can hold a typed reference that will
// also accept Mb_VictoryScreenUI when that is implemented later.
//
// CANVAS LAYOUT — set this up in the Unity Editor:
//   Defeat Screen Canvas  ← this GameObject; starts INACTIVE (SetActive false)
//   ├── Overlay Panel     ← full-screen black Image; attach OverlayCanvasGroup here
//   ├── Defeat Title      ← TMP_Text; set to "DEFEAT" in the Inspector
//   ├── Message Text      ← TMP_Text; left blank — populated at runtime by Show()
//   └── Main Menu Button  ← Button; wire OnClick → OnMainMenuClicked() on this component
//
// HOW THE FADE WORKS:
//   Show() activates the Canvas, sets OverlayCanvasGroup.alpha to 0, then a coroutine
//   ticks alpha from 0 → 1 over FadeDuration seconds. Once fully opaque (black screen
//   covering gameplay), the title and message are revealed on top.
//   Using CanvasGroup.alpha instead of per-element fades means the entire overlay
//   (including any future child elements) fades together as one unit.
//
// INSPECTOR SETUP:
//   - OverlayCanvasGroup: the CanvasGroup component on the black full-screen Image Panel.
//   - DefeatTitleText: set to "DEFEAT" in the Inspector — never changed at runtime.
//   - DefeatMessageText: leave blank — Show() populates it.
//   - MainMenuButton: wire its OnClick to OnMainMenuClicked() on this component.
//   - FadeDuration: seconds for the alpha fade (default 1f).
//   - MainMenuSceneName: must match your main menu scene name in File > Build Settings.
//     TODO: Update "MainMenu" to match your actual scene name.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Mb_DefeatScreenUI : Mb_EndScreenUI
{

    #region Inspector Fields            //----------------------------------------

    [Header("UI Elements")]
    [Tooltip("CanvasGroup on the full-screen black overlay Panel. Used for alpha fade control.")]
    [SerializeField] private CanvasGroup _OverlayCanvasGroup;

    [Tooltip("TMP_Text for the large defeat title. Set to 'DEFEAT' in the Inspector — " +
             "this text is never changed at runtime.")]
    [SerializeField] private TMP_Text _DefeatTitleText;

    [Tooltip("TMP_Text for the contextual defeat message. Leave blank — Show() populates it.")]
    [SerializeField] private TMP_Text _DefeatMessageText;

    [Tooltip("The Main Menu return button. Wire its OnClick event to OnMainMenuClicked() " +
             "on this component in the Inspector.")]
    [SerializeField] private Button _MainMenuButton;

    [Header("Settings")]
    [Tooltip("Seconds for the black overlay to fade from transparent to fully opaque.")]
    [SerializeField] private float _FadeDuration = 1f;

    [Tooltip("The exact scene name for the main menu. Must match an entry in File > Build Settings.")]
    [SerializeField] private string _MainMenuSceneName = "MainMenu";
    // TODO: Update "MainMenu" to match your actual scene name in Build Settings.

    #endregion                          //----------------------------------------


    #region Mb_EndScreenUI              //----------------------------------------

    /// <summary>
    /// Called by Mb_DefeatManager after the defeat sequence finishes.
    /// Activates this Canvas and fades in the overlay, then shows the message.
    /// </summary>
    public override void Show(string message)
    {
        // Activate the Canvas — it starts inactive in the scene so it's invisible
        // until defeat is actually triggered.
        gameObject.SetActive(true);


        Mb_AudioManager.PlayUI(UISFX.UI_StageDefeat);


        // Populate the message before the fade completes so text is ready the moment
        // it becomes visible — no pop-in delay.
        if (_DefeatMessageText != null)
            _DefeatMessageText.text = message;

        if (_DefeatTitleText == null)
            Debug.LogWarning("[Mb_DefeatScreenUI] DefeatTitleText is not assigned in the Inspector.");

        if (_OverlayCanvasGroup == null)
        {
            Debug.LogError("[Mb_DefeatScreenUI] OverlayCanvasGroup is not assigned. " +
                           "Assign the CanvasGroup on the black overlay Panel.");
            return;
        }

        // Keep title and message hidden until the black overlay is fully opaque —
        // prevents text from appearing over the gameplay view mid-fade.
        SetTextVisibility(false);

        StartCoroutine(FadeInRoutine());
    }

    #endregion                          //----------------------------------------


    #region Fade Coroutine              //----------------------------------------

    private IEnumerator FadeInRoutine()
    {
        // Start the overlay at fully transparent
        _OverlayCanvasGroup.alpha = 0f;

        float elapsed = 0f;

        // Fade the overlay from 0 → 1 over FadeDuration seconds.
        // Time.unscaledDeltaTime is used as a safety net — Mb_DefeatManager restores
        // timeScale = 1f before calling Show(), but unscaled delta ensures this works
        // correctly even if timeScale is non-standard for any reason.
        while (elapsed < _FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _OverlayCanvasGroup.alpha = Mathf.Clamp01(elapsed / _FadeDuration);
            yield return null;
        }

        // Snap to exactly 1 to avoid floating point drift leaving a thin gap
        _OverlayCanvasGroup.alpha = 1f;

        // Overlay is now fully opaque — gameplay view is completely hidden.
        // Safe to reveal the defeat title and message over the black screen.
        SetTextVisibility(true);
    }


    private void SetTextVisibility(bool visible)
    {
        // Toggle each text element's GameObject rather than its alpha so that
        // any future animations or child components on these elements also respond correctly.
        if (_DefeatTitleText != null)
            _DefeatTitleText.gameObject.SetActive(visible);

        if (_DefeatMessageText != null)
            _DefeatMessageText.gameObject.SetActive(visible);
    }

    #endregion                          //----------------------------------------


    #region Button Handler              //----------------------------------------

    /// <summary>
    /// Called by the Main Menu button's OnClick event (wire in Inspector).
    /// Resets timeScale as a safety measure before the scene transition — covers
    /// any edge case where it wasn't fully restored upstream.
    /// </summary>
    public void OnMainMenuClicked()
    {
        // Safety reset — always ensure timeScale is normal before leaving the scene.
        // This is a redundant safeguard; Mb_DefeatManager restores it before Show(),
        // but an explicit reset here costs nothing and prevents subtle bugs if the
        // call order ever changes during future development.
        Time.timeScale = 1f;

        // Load the main menu scene and set the correct game state.
        // We don't use SceneLoader.LoadStage() here because that method sets
        // GameState.Playing — incorrect for a return to the main menu.
        SceneManager.LoadScene(_MainMenuSceneName);
        GameManager.Instance.ChangeState(GameState.MainMenu);
    }

    #endregion                          //----------------------------------------
}