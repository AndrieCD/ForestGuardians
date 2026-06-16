using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Mb_PauseMenuUI : MonoBehaviour
{
    [Header("Settings Canvas")]
    [SerializeField] private GameObject _SettingsCanvas;

    [Header("Buttons")]
    [SerializeField] private Button _SettingsButton;
    [SerializeField] private Button _ContinueButton;
    [SerializeField] private Button _ExitGameButton;
    [SerializeField] private Button _GiveUpButton;


    public void OnSettingsClicked()
    {
        _SettingsCanvas.SetActive(true);
    }

    public void OnContinueClicked()
    {
        Mb_PauseManager.Instance.SetPause(false);
    }

    public void OnExitGameClicked()
    {
        // TODO: Show confirmation panel first
        Application.Quit();
    }

    public void OnGiveUpClicked()
    {
        // TODO: Show confirmation panel first

        // Go back to main menu
        SceneLoader.Instance.LoadMainMenu();
    }

    


}
