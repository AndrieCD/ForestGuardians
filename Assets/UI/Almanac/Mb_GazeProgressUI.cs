// Mb_GazeProgressUI.cs
// A single shared radial progress ring near the crosshair/reticle.
// Reflects gaze progress toward collecting whichever Mb_WildlifeAnimal
// is currently under the player's crosshair.
//
// WHY A SHARED SINGLETON INSTEAD OF PER-ANIMAL:
//   Only one animal can be gazed at at a time (the raycast hits one collider).
//   A single screen-space UI element avoids instantiating a World Space Canvas
//   on every wildlife prefab — cheaper and simpler to maintain.
//
// HOW IT'S DRIVEN:
//   Mb_WildlifeAnimal calls Mb_GazeProgressUI.ReportGaze(progress01) every
//   frame it is actively being gazed at, and calls HideRing() when gaze stops
//   or completes. This script never polls — it's purely reactive.
//
// Inspector setup:
//   - Place this component on the HUD Canvas, on the same GameObject as the
//     radial ring Image (or a parent of it — RingImage is assigned directly).
//   - Assign RingImage — the Image component with Image Type = Filled,
//     Fill Method = Radial 360.
//   - The ring GameObject should start INACTIVE in the hierarchy.

using UnityEngine;
using UnityEngine.UI;

public class Mb_GazeProgressUI : MonoBehaviour
{
    public static Mb_GazeProgressUI Instance { get; private set; }


    [Header("UI Reference")]
    [Tooltip("The radial fill Image. Image Type must be set to 'Filled' " +
             "with Fill Method 'Radial 360' in the Inspector.")]
    [SerializeField] private Image RingImage;


    private void Awake()
    {
        // Simple scene-local singleton — no DontDestroyOnLoad needed since
        // this lives on the in-stage HUD Canvas and is rebuilt each stage load
        Instance = this;

        // Ensure the ring starts hidden — gaze hasn't begun yet
        if (RingImage != null)
            RingImage.gameObject.SetActive(false);
    }


    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    /// <summary>
    /// Called by Mb_WildlifeAnimal every frame it is being actively gazed at.
    /// Shows the ring if hidden, and updates its fill amount.
    /// </summary>
    /// <param name="progress01">Gaze progress from 0 (just started) to 1 (collected).</param>
    public void ReportGaze(float progress01)
    {
        if (RingImage == null) return;

        if (!RingImage.gameObject.activeSelf)
            RingImage.gameObject.SetActive(true);

        RingImage.fillAmount = Mathf.Clamp01(progress01);
    }


    /// <summary>
    /// Called by Mb_WildlifeAnimal when gaze is broken (player looked away)
    /// or when collection completes. Hides the ring and resets fill.
    /// </summary>
    public void HideRing()
    {
        if (RingImage == null) return;

        RingImage.gameObject.SetActive(false);
        RingImage.fillAmount = 0f;
    }
}