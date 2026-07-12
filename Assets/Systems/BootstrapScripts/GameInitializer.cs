using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameInitializer : MonoBehaviour
{
    public static GameInitializer Instance { get; private set; }

    [Header("Startup Cutscene")]
    [SerializeField] private bool PlayPrologueCutsceneOnStart = true;

    void Awake( )
    {
        Debug.Log("GameInitializer Awake - setting up logging and initializing core systems.");

        Application.logMessageReceived += (condition, stackTrace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception)
                System.IO.File.AppendAllText(
                    Application.persistentDataPath + "/build_log.txt",
                    $"[{type}] {condition}\n{stackTrace}\n\n"
                );
        };



        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        DontDestroyOnLoad(gameObject);

        // Initialize core systems
        GameManager.Instance.Initialize( );
        UIManager.Instance.Initialize( );

        if (PlayPrologueCutsceneOnStart)
        {
            SceneLoader.Instance.LoadCutscene(E_CutsceneId.Prologue, E_CutsceneDestination.MainMenu);
            return;
        }

        SceneLoader.Instance.LoadMainMenu();
    }
}
