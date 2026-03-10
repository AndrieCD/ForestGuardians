using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameInitializer : MonoBehaviour
{
    public static GameInitializer Instance { get; private set; }
    void Awake( )
    {


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
        SceneManager.LoadScene("ApplicationGUI");
    }
}