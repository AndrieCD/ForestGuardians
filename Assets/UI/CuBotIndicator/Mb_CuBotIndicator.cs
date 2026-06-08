// Mb_CuBotIndicator.cs
// A single screen-edge indicator UI element that points toward one off-screen CuBot.
//
// HOW IT WORKS:
//   - Mb_CuBotIndicatorManager creates and pools these.
//   - Each frame, the manager calls UpdatePosition() with the CuBot's world position.
//   - This script projects that world position to screen space and decides:
//       • If the CuBot is on-screen  → hide the indicator
//       • If the CuBot is off-screen → show and pin to the nearest screen edge
//   - Pulse animation (brief scale pop) is triggered externally by the manager
//     via TriggerPulse(). This keeps all timing logic in one place.
//   - Assign() binds this indicator to a CuBot. Release() unbinds it for pool return.
//
// Inspector setup:
//   - This script lives on a prefab that contains a RectTransform and an Image.
//   - The Image is the visual dot/arrow for the indicator.
//   - All sizing and color is set on the prefab — this script drives position only.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Mb_CuBotIndicator : MonoBehaviour
{
    #region References          //----------------------------------------

    // The Image component that renders the indicator dot/arrow
    // Assigned in Awake — must be on this same GameObject
    private Image _image;

    // RectTransform of this indicator — used to set screen-edge position
    private RectTransform _rectTransform;

    // Cached camera — passed in from the manager so we never call Camera.main per frame
    private Camera _mainCamera;

    #endregion                  //----------------------------------------


    #region State               //----------------------------------------

    // The CuBot this indicator is currently tracking
    // Null when this indicator is sitting idle in the pool
    private Transform _trackedCuBot;

    // Whether a pulse coroutine is currently running
    // Prevents stacking multiple pulses if TriggerPulse is called rapidly
    private bool _isPulsing = false;

    // True while a pause is active — stops position updates and coroutines
    private bool _isPaused = false;

    #endregion                  //----------------------------------------


    #region Configuration       //----------------------------------------

    // These are set by the manager via Configure() after instantiation
    // so all indicators share the same tuning values from one Inspector source
    private float _edgeMargin;
    private float _pulseScale;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rectTransform = GetComponent<RectTransform>();

        if (_image == null)
            Debug.LogError($"[Mb_CuBotIndicator] No Image component found on {gameObject.name}.");
    }


    private void OnEnable()
    {
        Mb_PauseManager.OnPaused += HandlePause;
        Mb_PauseManager.OnResumed += HandleResume;
    }


    private void OnDisable()
    {
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;
    }

    #endregion                  //----------------------------------------


    #region Public API          //----------------------------------------

    /// <summary>
    /// Called once by the manager after instantiation to pass shared config values.
    /// Keeps tuning data in one place (the manager's Inspector) instead of per-prefab.
    /// </summary>
    public void Configure(Camera mainCamera, float edgeMargin, float pulseScale)
    {
        _mainCamera = mainCamera;
        _edgeMargin = edgeMargin;
        _pulseScale = pulseScale;
    }


    /// <summary>
    /// Binds this indicator to a CuBot and makes it visible.
    /// Called by the manager when a new CuBot spawns.
    /// </summary>
    public void Assign(Transform cuBotTransform)
    {
        _trackedCuBot = cuBotTransform;
        gameObject.SetActive(true);

        // Reset scale in case a previous pulse left it mid-animation
        transform.localScale = Vector3.one;
    }


    /// <summary>
    /// Unbinds the indicator and hides it. Called when the tracked CuBot dies.
    /// The manager calls this before returning the indicator to the pool.
    /// </summary>
    public void Release()
    {
        _trackedCuBot = null;
        _isPulsing = false;
        transform.localScale = Vector3.one;
        gameObject.SetActive(false);
    }


    /// <summary>
    /// Updates the indicator's screen-edge position for this frame.
    /// Called every frame by the manager — keeps the update loop centralized.
    /// Hides the indicator when the CuBot is on-screen.
    /// </summary>
    public void UpdatePosition()
    {
        // Don't update if paused, unassigned, or camera is missing
        if (_isPaused || _trackedCuBot == null || _mainCamera == null) return;

        // Project the CuBot's world position into viewport space.
        // Viewport space: (0,0) = bottom-left, (1,1) = top-right, z = distance from camera.
        // z < 0 means the point is behind the camera — treat as off-screen.
        Vector3 viewportPos = _mainCamera.WorldToViewportPoint(_trackedCuBot.position);

        bool isOnScreen = viewportPos.z > 0f
            && viewportPos.x >= 0f && viewportPos.x <= 1f
            && viewportPos.y >= 0f && viewportPos.y <= 1f;

        if (isOnScreen)
        {
            // CuBot is visible — hide the indicator
            if (_image.enabled) _image.enabled = false;
            return;
        }

        // CuBot is off-screen — show the indicator and pin it to the screen edge
        if (!_image.enabled) _image.enabled = true;

        // If the CuBot is behind the camera, flip the viewport position so
        // the indicator appears on the correct side of the screen
        if (viewportPos.z < 0f)
        {
            viewportPos.x = 1f - viewportPos.x;
            viewportPos.y = 1f - viewportPos.y;

            // When the target is behind the camera, the vertical viewport value
            // will move as the player looks up/down which causes the indicator
            // to slide away from the screen edge. Instead, pin the indicator
            // to the nearest vertical screen edge so it stays stuck to top/bottom.
            viewportPos.y = (viewportPos.y < 0.5f) ? 0f : 1f;
        }

        // Convert viewport (0-1) to screen pixels, then clamp to screen edges
        // with the configured margin so the indicator stays fully on-screen
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        float screenX = Mathf.Clamp(
            viewportPos.x * screenWidth,
            _edgeMargin,
            screenWidth - _edgeMargin
        );

        float screenY = Mathf.Clamp(
            viewportPos.y * screenHeight,
            _edgeMargin,
            screenHeight - _edgeMargin
        );

        // Screen Space — Overlay canvas uses screen pixels directly as anchor position
        _rectTransform.position = new Vector3(screenX, screenY, 0f);
    }


    /// <summary>
    /// Plays a brief scale-pop animation.
    /// Called by the manager on spawn and on the shared pulse interval.
    /// Does nothing if a pulse is already running — no stacking.
    /// </summary>
    public void TriggerPulse()
    {
        // Only pulse if visible and not already pulsing
        if (_isPulsing || _trackedCuBot == null) return;
        if (!_image.enabled) return;

        StartCoroutine(PulseRoutine());
    }

    #endregion                  //----------------------------------------


    #region Coroutines          //----------------------------------------

    // Scales up to _pulseScale then back to 1 over a short duration.
    // Simple lerp-based animation — no dependency on DOTween or animation curves.
    private IEnumerator PulseRoutine()
    {
        _isPulsing = true;

        const float PULSE_UP_DURATION = 0.12f;
        const float PULSE_DOWN_DURATION = 0.18f;

        float elapsed = 0f;

        // Scale up
        while (elapsed < PULSE_UP_DURATION)
        {
            // Skip time advancement while paused — coroutines keep running when
            // timeScale = 0, so we guard manually here
            if (!_isPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / PULSE_UP_DURATION;
                transform.localScale = Vector3.one * Mathf.Lerp(1f, _pulseScale, t);
            }

            yield return null;
        }

        elapsed = 0f;

        // Scale back down
        while (elapsed < PULSE_DOWN_DURATION)
        {
            if (!_isPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / PULSE_DOWN_DURATION;
                transform.localScale = Vector3.one * Mathf.Lerp(_pulseScale, 1f, t);
            }

            yield return null;
        }

        transform.localScale = Vector3.one;
        _isPulsing = false;
    }

    #endregion                  //----------------------------------------


    #region Pause Handling      //----------------------------------------

    private void HandlePause() => _isPaused = true;
    private void HandleResume() => _isPaused = false;

    #endregion                  //----------------------------------------
}