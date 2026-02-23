using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public void OnPlayClicked( )
    {
        GameManager.Instance.ChangeState(GameState.LoadingStage);
        SceneManager.LoadScene("TestScene");

        Debug.Log("Play button clicked - loading stage...");
    }

    public void OnGuardiansClicked( )
    {
        // Show Guardians menu (could be another canvas or scene)
        Debug.Log("Guardians button clicked - showing guardians menu...");
    }

    public void OnAlmanacClicked( )
    {
        // Show Almanac menu
        Debug.Log("Almanac button clicked - showing almanac menu...");
    }

    public void OnQuitClicked( )
    {
        Application.Quit( );
    }

}