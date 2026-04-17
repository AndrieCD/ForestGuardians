using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Main Menu")]
    public GameObject MainMenuCanvas;

    [Header("In-Game HUD")]
    public GameObject HUDCanvas;

    [Header("Pause Menu")]
    public GameObject PauseMenuCanvas;

    public void Initialize( )
    {
        UpdateUI(GameManager.Instance.CurrentState);
        GameManager.Instance.OnGameStateChanged += UpdateUI;

        Debug.Log("UIManager initialized.");
    }

    private void Awake( )
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void UpdateUI(GameState state)
    {
        // Main menu
        if (MainMenuCanvas != null)
            MainMenuCanvas.SetActive(state == GameState.MainMenu);

        // HUD
        if (HUDCanvas != null)
            HUDCanvas.SetActive(state == GameState.Playing || state == GameState.Paused);

        // Pause menu
        if (PauseMenuCanvas != null)
            PauseMenuCanvas.SetActive(state == GameState.Paused);

        // Rewards Panel
        // ...
    }




    public void RegisterHUD(GameObject hud)
    {
        HUDCanvas = hud;
        UpdateUI(GameManager.Instance.CurrentState);
    }

    public void RegisterPauseMenu(GameObject pauseMenu)
    {
        PauseMenuCanvas = pauseMenu;
        UpdateUI(GameManager.Instance.CurrentState);
    }

    public void RegisterMainMenu(GameObject mainMenu)
    {
        MainMenuCanvas = mainMenu;
        UpdateUI(GameManager.Instance.CurrentState);
    }

    public void UnregisterHUD(GameObject hud)
    {
        if (HUDCanvas == hud) HUDCanvas = null;
    }
}