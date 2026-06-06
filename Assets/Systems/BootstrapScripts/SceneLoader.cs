using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The single authority for all scene transitions in Forest Guardians.
/// All scene loads must go through here — never call SceneManager.LoadScene directly.
///
/// Handles: loading screen, GameState transitions, and async loading so the
/// one-frame timing issue (Build vs Editor) is handled in one place for all scenes.
///
/// Lives on the Bootstrap GameObject alongside GameManager and UIManager.
/// Call via SceneLoader.Instance from anywhere.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Loading Screen")]
    // Assign a simple canvas with a spinner or progress bar in the Inspector.
    // Leave null if you don't have one yet — it's guarded below.
    [SerializeField] private GameObject _LoadingScreen;

    // Prevents double-loads if something calls LoadX twice quickly
    private bool _isLoading = false;


    private void Awake()
    {
        Debug.Log("SceneLoader Awake - ready to manage scene transitions.");

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    // -------------------------------------------------------------------------
    // Public API — call these instead of SceneManager.LoadScene directly
    // -------------------------------------------------------------------------

    /// <summary>Loads the main menu.</summary>
    public void LoadMainMenu()
    {
        Load("ApplicationGUI", GameState.MainMenu);
    }

    /// <summary>Loads a stage by number (1, 2, or 3).</summary>
    public void LoadStage(int stageNumber)
    {
        // Scene names follow the convention "Stage1", "Stage2", "Stage3"
        // Add a range guard so a typo or bad call doesn't crash silently
        if (stageNumber < 1 || stageNumber > 3)
        {
            Debug.LogError($"[SceneLoader] Invalid stage number: {stageNumber}. Must be 1–3.");
            return;
        }

        Load($"Stage{stageNumber}", GameState.LoadingStage);
    }

    /// <summary>Loads a cutscene by name.</summary>
    public void LoadCutscene(string cutsceneName)
    {
        // Cutscene scenes follow the convention "Cutscene_Intro", "Cutscene_Stage2", etc.
        Load(cutsceneName, GameState.LoadingStage);
    }

    /// <summary>Reloads the current scene — useful for a retry button after defeat.</summary>
    public void ReloadCurrentScene()
    {
        Load(SceneManager.GetActiveScene().name, GameState.LoadingStage);
    }


    // -------------------------------------------------------------------------
    // Core Load Logic
    // -------------------------------------------------------------------------

    private void Load(string sceneName, GameState stateBeforeLoad)
    {
        if (_isLoading)
        {
            Debug.LogWarning($"[SceneLoader] Already loading a scene. Ignoring request to load '{sceneName}'.");
            return;
        }

        StartCoroutine(LoadRoutine(sceneName, stateBeforeLoad));
    }


    private IEnumerator LoadRoutine(string sceneName, GameState stateBeforeLoad)
    {
        _isLoading = true;

        // Set state and show loading screen before anything unloads
        GameManager.Instance.ChangeState(stateBeforeLoad);
        SetLoadingScreenVisible(true);

        // Let the loading screen render for at least one frame before the
        // heavy async load begins — prevents a visible hitch on the old scene
        yield return null;

        // Async load — doesn't block the main thread, so the loading screen stays responsive
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        // Hold at 90% until we're ready to activate — gives us control over the handoff
        operation.allowSceneActivation = false;

        // Wait until Unity has finished loading in the background (reaches 90%)
        while (operation.progress < 0.9f)
            yield return null;

        // Scene is ready — activate it
        operation.allowSceneActivation = true;

        // Wait one more frame for all Awake() and OnEnable() calls in the new scene to complete
        yield return null;

        // Hide loading screen now that the new scene is alive
        SetLoadingScreenVisible(false);

        _isLoading = false;

        // Note: GameState.Playing is NOT set here.
        // Mb_StageManager.StartStage() owns that transition for stage scenes.
        // For non-stage scenes (MainMenu), GameInitializer or the scene itself sets the state.
    }


    private void SetLoadingScreenVisible(bool visible)
    {
        if (_LoadingScreen != null)
            _LoadingScreen.SetActive(visible);
    }
}