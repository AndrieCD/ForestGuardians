using UnityEngine.SceneManagement;
using UnityEngine;

public class SceneLoader : MonoBehaviour
{
    public void LoadStage(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
        // Change gamestate after loading the scene
        GameManager.Instance.ChangeState(GameState.Playing);

        Debug.Log("Loading stage: " + sceneName);
    }
}