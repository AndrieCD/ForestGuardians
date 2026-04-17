using UnityEngine;

public class Sc_SceneUIBinder : MonoBehaviour
{
    public GameObject HUDCanvas;
    public GameObject PauseMenuCanvas;

    private void Start()
    {
        if (UIManager.Instance == null) return;

        if (HUDCanvas != null)
            UIManager.Instance.RegisterHUD(HUDCanvas);

        if (PauseMenuCanvas != null)
            UIManager.Instance.RegisterPauseMenu(PauseMenuCanvas);
    }

    private void OnDestroy()
    {
        if (UIManager.Instance == null) return;

        if (HUDCanvas != null)
            UIManager.Instance.UnregisterHUD(HUDCanvas);
    }
}