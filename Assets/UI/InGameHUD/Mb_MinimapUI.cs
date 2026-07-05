// Mb_MinimapUI.cs
// Drives the minimap panel on the HUD Canvas.
//
// HOW IT WORKS:
//   This script handles two things:
//
//   1. CAMERA FOLLOW
//      The MinimapCamera is already parented to the main camera in your prototype,
//      so it follows automatically. This script just manages its orthographic size
//      (zoom) when an optional camera reference is assigned.
//
//   2. MAP BORDER / PANEL VISIBILITY
//      The RawImage displaying the RenderTexture, the border frame, and any
//      overlay elements are shown/hidden via GameManager.OnGameStateChanged.
//
//   Icon rendering is handled entirely by the minimap camera seeing the
//   SpriteRenderer icon child GOs on the Minimap layer directly — no UI
//   coordinate conversion needed. This is simpler and more accurate than
//   projecting world positions onto a RawImage.
//
// SETUP CHECKLIST (do these in the Editor):
//   [ ] MinimapCamera culling mask: ALL layers EXCEPT player/enemy meshes, VFX, UI.
//       INCLUDE: Terrain, Path, Ground, Water, Environment, Minimap.
//   [ ] MinimapCamera: Orthographic, Clear Flags = Solid Color, bg = dark color.
//   [ ] MinimapCamera renders to a RenderTexture asset (create one: 256x256 or 512x512).
//   [ ] Assign that RenderTexture to the RawImage on the HUD Canvas.
//   [ ] MinimapCamera is parented to the main camera (already done in your prototype).
//   [ ] MinimapCamera local position: straight up from main cam, e.g. (0, 50, 0).
//       Adjust Y so the orthographic view covers the area you want at default zoom.
//   [ ] All icon child GOs are on the Minimap layer.
//   [ ] All terrain/path/environment GOs are on their normal layers (NOT Minimap).
//
// Inspector Setup:
//   - MinimapCamera: optional reference for default zoom setup.
//   - DefaultOrthographicSize: starting zoom level (world units half-height visible).
//   - MinimapPanel: the root panel GO containing the RawImage + border — toggled on state change.

using UnityEngine;

public class Mb_MinimapUI : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Header("Camera")]
    [Tooltip("The orthographic minimap camera parented to the main camera.")]
    [SerializeField] private Camera MinimapCamera;

    // TODO: Tune DefaultOrthographicSize — start around 40–60 for a typical Unity stage
    // and adjust until the map layout fills the minimap panel comfortably.
    // Larger value = more of the world visible = more zoomed out.
    [Tooltip("Orthographic size (world-unit half-height) for the default zoom level.")]
    [SerializeField] private float DefaultOrthographicSize = 50f;

    [Header("Panel")]
    [Tooltip("Root panel GO (RawImage + border frame). Shown only during Playing/Paused.")]
    [SerializeField] private GameObject MinimapPanel;

    //[Header("Optional — Zoom")]
    //// TODO: Hook ZoomIn/ZoomOut to input or UI buttons if a zoom feature is desired later.
    //// For now these are just the clamped range so the inspector value can't be set out of range.
    //[SerializeField] private float MinZoom = 20f;
    //[SerializeField] private float MaxZoom = 100f;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        if (MinimapCamera != null)
            MinimapCamera.orthographicSize = DefaultOrthographicSize;

        if (MinimapPanel == null)
            Debug.LogError("[Mb_MinimapUI] MinimapPanel is not assigned.");
    }


    private void OnEnable()
    {
        //if (GameManager.Instance != null)
        //{
        //    GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        //    GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        //}
    }


    private void OnDisable()
    {
        //if (GameManager.Instance != null)
        //    GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }

    #endregion                  //----------------------------------------


    #region Public API          //----------------------------------------

    /// <summary>
    /// Adjusts the minimap zoom level. Positive delta = zoom in, negative = zoom out.
    /// Call from a UI button or input binding if zoom is added later.
    /// </summary>
    public void AdjustZoom(float delta)
    {
        if (MinimapCamera == null) return;

        //float newSize = Mathf.Clamp(
        //    MinimapCamera.orthographicSize - delta,
        //    MinZoom,
        //    MaxZoom
        //);
        //MinimapCamera.orthographicSize = newSize;
    }

    #endregion                  //----------------------------------------


    #region Show / Hide         //----------------------------------------

    //private void HandleGameStateChanged(GameState newState)
    //{
    //    if (MinimapPanel == null) return;

    //    bool shouldShow = newState == GameState.Playing || newState == GameState.Paused;
    //    MinimapPanel.SetActive(shouldShow);
    //}

    #endregion                  //----------------------------------------
}
