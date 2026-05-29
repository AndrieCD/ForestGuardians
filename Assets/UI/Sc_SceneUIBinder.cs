using UnityEngine;

public class Sc_SceneUIBinder : MonoBehaviour
{
    [Header("Scene UI References")]
    [SerializeField] public GameObject HUDCanvas;
    [SerializeField] public GameObject PauseMenuCanvas;

    private void Start()
    {
        // Guard: if UIManager isn't alive, someone launched this scene directly
        // without going through Bootstrap. Always test via Bootstrap in builds.
        if (UIManager.Instance == null)
        {
            Debug.LogError("[Sc_SceneUIBinder] UIManager.Instance is null. " +
                           "This scene must be loaded through Bootstrap, not directly. " +
                           "In the Editor, always press Play from the Bootstrap scene.");
            return;
        }

        if (HUDCanvas != null)
            UIManager.Instance.RegisterHUD(HUDCanvas);
        else
            Debug.LogWarning("[Sc_SceneUIBinder] HUDCanvas is not assigned in the Inspector.");

        if (PauseMenuCanvas != null)
            UIManager.Instance.RegisterPauseMenu(PauseMenuCanvas);
        else
            Debug.LogWarning("[Sc_SceneUIBinder] PauseMenuCanvas is not assigned in the Inspector.");
    }

    private void OnDestroy()
    {
        if (UIManager.Instance == null) return;

        if (HUDCanvas != null)
            UIManager.Instance.UnregisterHUD(HUDCanvas);
    }
}