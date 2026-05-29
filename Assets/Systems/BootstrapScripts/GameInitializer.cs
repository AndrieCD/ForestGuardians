using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameInitializer : MonoBehaviour
{
    public static GameInitializer Instance { get; private set; }
    void Awake( )
    {
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

        // Load main menu
        SceneLoader.Instance.LoadMainMenu();
    }
}