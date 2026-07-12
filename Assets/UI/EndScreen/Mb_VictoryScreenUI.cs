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

    [Tooltip("Credits scene name loaded after the final restoration cutscene.")]
    [SerializeField] private string _CreditsSceneName = "Credits";

    #endregion                          //----------------------------------------


    #region Mb_EndScreenUI              //----------------------------------------

    /// <summary>
    /// Called by Mb_VictoryManager after the victory sequence finishes.
    /// Activates the Canvas and fades in the overlay, then reveals the message.
    /// </summary>
    public override void Show(string message)
    {
        gameObject.SetActive(true);

        Mb_AudioManager.PlayUI(UISFX.UI_StageClear);


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

        if (_ContinueButton != null)
            _ContinueButton.gameObject.SetActive(HasContinueRoute());

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
    /// Loads the next story cutscene, then that cutscene continues to its destination.
    /// </summary>
    public void OnContinueClicked()
    {
        // Safety reset — ensures timeScale is normal before any scene transition.
        Time.timeScale = 1f;

        if (!TryGetContinueRoute(
            out E_CutsceneId cutsceneId,
            out E_CutsceneDestination destination,
            out int targetStageNumber,
            out string destinationSceneName))
        {
            Debug.LogWarning("[Mb_VictoryScreenUI] Continue clicked with no valid route.");
            return;
        }

        SceneLoader.Instance.LoadCutscene(
            cutsceneId,
            destination,
            targetStageNumber,
            destinationSceneName);
    }


    /// <summary>
    /// Called by the Main Menu button's OnClick event.
    /// </summary>
    public void OnMainMenuClicked()
    {
        // Safety reset — same rationale as Mb_DefeatScreenUI.
        Time.timeScale = 1f;

        Sc_CutsceneSession.ClearAll();
        Sc_RunSession.Clear();

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadMainMenu();
            return;
        }

        Debug.LogError("[Mb_VictoryScreenUI] SceneLoader.Instance is missing. Cannot return to main menu.");
    }

    #endregion                          //----------------------------------------


    #region Continue Route              //----------------------------------------

    private bool HasContinueRoute()
    {
        return TryGetContinueRoute(
            out E_CutsceneId cutsceneId,
            out E_CutsceneDestination destination,
            out int targetStageNumber,
            out string destinationSceneName)
            && cutsceneId != E_CutsceneId.None
            && destination != E_CutsceneDestination.None;
    }


    private bool TryGetContinueRoute(
        out E_CutsceneId cutsceneId,
        out E_CutsceneDestination destination,
        out int targetStageNumber,
        out string destinationSceneName)
    {
        cutsceneId = E_CutsceneId.None;
        destination = E_CutsceneDestination.None;
        targetStageNumber = 0;
        destinationSceneName = string.Empty;

        switch (Sc_RunSession.SelectedStageNumber)
        {
            case Sc_RunSession.TUTORIAL_STAGE:
                cutsceneId = E_CutsceneId.Awakening;
                destination = E_CutsceneDestination.Stage;
                targetStageNumber = Sc_RunSession.STAGE_1;
                return true;

            case Sc_RunSession.STAGE_1:
                cutsceneId = E_CutsceneId.KarstsRetreat;
                destination = E_CutsceneDestination.Stage;
                targetStageNumber = Sc_RunSession.STAGE_2;
                return true;

            case Sc_RunSession.STAGE_2:
                cutsceneId = E_CutsceneId.RuinsOfWar;
                destination = E_CutsceneDestination.Stage;
                targetStageNumber = Sc_RunSession.STAGE_3;
                return true;

            case Sc_RunSession.STAGE_3:
                cutsceneId = E_CutsceneId.Restoration;
                destination = E_CutsceneDestination.Credits;
                destinationSceneName = _CreditsSceneName;
                return true;
        }

        return false;
    }

    #endregion                          //----------------------------------------
}
