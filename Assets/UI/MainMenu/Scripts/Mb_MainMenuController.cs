// Mb_MainMenuController.cs
// Central canvas switcher for the main menu scene.
// Owns references to all canvases on the main menu and handles
// activation / deactivation when the player navigates between screens.
//
// REPLACES the existing MainMenuController.cs — all original button method
// names are preserved so existing Inspector wiring does not need to change:
//   OnPlayClicked()       → now opens Stage Selection instead of loading Stage 1
//   OnGuardiansClicked()  → opens Characters canvas (stub — content TODO)
//   OnAlmanacClicked()    → opens Almanac canvas
//   OnQuitClicked()       → quits application (unchanged)
//
// CANVAS SWITCHING PATTERN:
//   Every Show___() method deactivates all canvases first, then activates
//   exactly one. This single-active-canvas rule prevents overlap bugs and
//   makes it easy to add new canvases later — just add a field and a method.
//
// GAME STATE:
//   All canvas switches call GameManager.Instance.ChangeState() so the
//   cursor, UI manager, and any state-dependent systems stay in sync.
//   // TODO: Add StageSelection and GuardianSelection to the GameState enum
//   //       in GameManager.cs once the team confirms those are needed as
//   //       distinct states. For now both use GameState.MainMenu since they
//   //       are sub-screens of the main menu and share the same cursor/input rules.
//
// Inspector setup:
//   - Assign all five canvas GameObjects in the Inspector.
//   - This component should live on a root GameObject in the main menu scene.
//   - Wire all Button.OnClick() events to the public methods on this component.
//   - The main menu canvas should be active by default in the scene;
//     all other canvases should start inactive.

using UnityEngine;

public class Mb_MainMenuController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    #region Inspector Fields        //----------------------------------------

    [Header("Canvases")]
    [Tooltip("The root main menu canvas — Play, Almanac, Characters, Exit buttons.")]
    [SerializeField] private GameObject MainMenuCanvas;

    [Tooltip("Stage selection screen — stone tablet with gem nodes.")]
    [SerializeField] private GameObject StageSelectionCanvas;

    [Tooltip("Guardian selection screen — portrait display and details panel.")]
    [SerializeField] private GameObject GuardianSelectionCanvas;

    [Tooltip("Wildlife Almanac book canvas.")]
    [SerializeField] private GameObject AlmanacCanvas;

    [Tooltip("Characters book canvas. " +
             "// TODO: Implement Characters canvas content — currently a stub.")]
    [SerializeField] private GameObject CharactersCanvas;

    [Tooltip("Settings canvas. Opens as an overlay on top of the main menu.")]
    [SerializeField] private GameObject SettingsCanvas;

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    #region Unity Lifecycle         //----------------------------------------

    private void Start()
    {
        // Defensive initialization — ensure only the main menu is visible
        // on scene load regardless of what was active in the Editor.
        // This prevents a designer accidentally leaving a sub-canvas active
        // in the scene from causing a visual glitch on first launch.
        ShowMainMenu();
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Canvas Navigation — Public (called by Button.OnClick in Inspector)
    // -------------------------------------------------------------------------

    #region Canvas Navigation       //----------------------------------------

    /// <summary>
    /// Shows the root main menu canvas.
    /// Called by Back buttons on Stage Selection and other sub-screens.
    /// </summary>
    public void ShowMainMenu()
    {
        Sc_CutsceneSession.ClearAll();
        Sc_RunSession.Clear();

        SetAllCanvasesInactive();
        SetActive(MainMenuCanvas, true);
        GameManager.Instance.ChangeState(GameState.MainMenu);
    }


    /// <summary>
    /// Shows the Stage Selection canvas.
    /// Notifies Mb_StageSelectionUI to refresh gem states when opening.
    /// </summary>
    public void ShowStageSelection()
    {
        SetAllCanvasesInactive();
        SetActive(StageSelectionCanvas, true);

        // Notify the stage selection UI to refresh unlock visuals every time
        // the screen opens — covers the case where the player completed a stage
        // and returned to the menu in the same session.
        Mb_StageSelectionUI stageUI =
            StageSelectionCanvas?.GetComponentInChildren<Mb_StageSelectionUI>();

        stageUI?.RefreshGems();

        // TODO: Add GameState.StageSelection to GameState enum in GameManager.cs
        //       and replace GameState.MainMenu here with it.
        GameManager.Instance.ChangeState(GameState.MainMenu);
    }


    /// <summary>
    /// Shows the Guardian Selection canvas.
    /// Called by Mb_StageSelectionUI when the player confirms a stage.
    /// </summary>
    public void ShowGuardianSelection()
    {
        SetAllCanvasesInactive();
        SetActive(GuardianSelectionCanvas, true);

        // Notify the guardian selection UI to reset to the default
        // (first guardian pre-selected) every time the screen opens.
        Mb_GuardianSelectionUI guardianUI =
            GuardianSelectionCanvas?.GetComponentInChildren<Mb_GuardianSelectionUI>();

        guardianUI?.ResetToDefault();

        // TODO: Add GameState.GuardianSelection to GameState enum in GameManager.cs
        //       and replace GameState.MainMenu here with it.
        GameManager.Instance.ChangeState(GameState.MainMenu);
    }


    /// <summary>
    /// Shows the Almanac canvas.
    /// </summary>
    public void ShowAlmanac()
    {
        SetAllCanvasesInactive();
        SetActive(AlmanacCanvas, true);
        GameManager.Instance.ChangeState(GameState.MainMenu);
    }


    /// <summary>
    /// Shows the Characters canvas.
    /// // TODO: Implement Characters canvas content.
    /// </summary>
    public void ShowCharacters()
    {
        SetAllCanvasesInactive();
        SetActive(CharactersCanvas, true);
        GameManager.Instance.ChangeState(GameState.MainMenu);
    }


    /// <summary>
    /// Shows the Settings canvas over the root main menu canvas.
    /// </summary>
    public void ShowSettings()
    {
        SetAllCanvasesInactive();
        SetActive(MainMenuCanvas, true);
        SetActive(SettingsCanvas, true);
        GameManager.Instance.ChangeState(GameState.MainMenu);
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Original Button Handlers (preserved from MainMenuController.cs)
    // All Inspector wiring for these four buttons stays valid.
    // -------------------------------------------------------------------------

    #region Original Button Handlers    //----------------------------------------

    /// <summary>
    /// Called by the Play button on the main menu.
    /// Previously loaded Stage 1 directly — now opens Stage Selection.
    /// </summary>
    public void OnPlayClicked()
    {
        ShowStageSelection();
        Debug.Log("[Mb_MainMenuController] Play clicked — opening Stage Selection.");
    }


    /// <summary>
    /// Called by the Characters button on the main menu.
    /// </summary>
    public void OnGuardiansClicked()
    {
        ShowCharacters();
        Debug.Log("[Mb_MainMenuController] Characters clicked — opening Characters canvas.");
    }


    /// <summary>
    /// Called by the Almanac button on the main menu.
    /// </summary>
    public void OnAlmanacClicked()
    {
        ShowAlmanac();
        Debug.Log("[Mb_MainMenuController] Almanac clicked — opening Almanac canvas.");
    }

    public void OnSettingsClicked()
    {
        ShowSettings();
        Debug.Log("[Mb_MainMenuController] Settings clicked - opening Settings canvas.");
    }

    public void OnTutorialClicked()
    {
        SceneLoader.Instance.LoadTutorial();
        Debug.Log("Tutorial button clicked - loading tutorial scene...");
    }


    /// <summary>
    /// Called by the Quit button on the main menu.
    /// Unchanged from the original MainMenuController.cs.
    /// </summary>
    public void OnQuitClicked()
    {
        Application.Quit();
        Debug.Log("[Mb_MainMenuController] Quit clicked.");
    }

    #endregion                      //----------------------------------------


    // -------------------------------------------------------------------------
    // Internal Helpers
    // -------------------------------------------------------------------------

    #region Helpers                 //----------------------------------------

    // Deactivates all known canvases in one call.
    // Always called before activating a new canvas — ensures only one
    // canvas is ever active at a time.
    private void SetAllCanvasesInactive()
    {
        SetActive(MainMenuCanvas, false);
        SetActive(StageSelectionCanvas, false);
        SetActive(GuardianSelectionCanvas, false);
        SetActive(AlmanacCanvas, false);
        SetActive(CharactersCanvas, false);
        SetActive(SettingsCanvas, false);
    }


    // Null-safe SetActive wrapper — prevents a missing Inspector assignment
    // from throwing a NullReferenceException during canvas switches.
    // Logs a warning so the missing assignment is easy to spot in the Console.
    private void SetActive(GameObject canvas, bool active)
    {
        if (canvas == null)
        {
            if (active)
                Debug.LogWarning("[Mb_MainMenuController] Tried to activate a canvas " +
                                 "that is not assigned in the Inspector.");
            return;
        }

        canvas.SetActive(active);
    }

    #endregion                      //----------------------------------------
}
