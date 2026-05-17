// Mb_PlayerFloatingText.cs
// A single floating combat text element on the player's HUD.
//
// HOW IT WORKS:
//   - Mb_PlayerFloatingTextManager pools these and calls Activate() when
//     damage, healing, shields, or status effects need to be displayed.
//   - On activation: the text is set, positioned at the anchor zone with a
//     small random horizontal offset, then slides upward while fading out.
//   - When the animation finishes, this element signals the manager to
//     return it to the pool via the OnReadyForPool callback.
//   - Pause-safe: uses Time.unscaledDeltaTime in the animation coroutine
//     so the animation doesn't freeze when timeScale = 0. Combat text that
//     was already animating before a pause will continue — this is intentional
//     and matches expected feel (damage numbers don't freeze mid-air).
//
// Inspector setup:
//   - This script lives on a prefab containing a RectTransform and a TMP_Text.
//   - No fields need to be assigned in the Inspector — everything is passed
//     in via Activate() at runtime by the manager.

using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class Mb_PlayerFloatingText : MonoBehaviour
{
    #region References          //----------------------------------------

    // The text component that displays the combat number or status label
    // Fetched once in Awake — must be on this same GameObject
    private TMP_Text _label;

    // RectTransform of this element — used to drive position during animation
    private RectTransform _rectTransform;

    #endregion                  //----------------------------------------


    #region State               //----------------------------------------

    // Fired when the animation finishes — manager subscribes to this
    // to know when to return this element to the pool.
    // Using an event instead of a direct manager reference keeps this
    // class decoupled from the manager.
    public event Action<Mb_PlayerFloatingText> OnReadyForPool;

    // True while an animation coroutine is running
    // Prevents double-activation on a still-animating element
    private bool _isAnimating = false;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        _label = GetComponent<TMP_Text>();
        _rectTransform = GetComponent<RectTransform>();

        if (_label == null)
            Debug.LogError($"[Mb_PlayerFloatingText] No TMP_Text found on {gameObject.name}.");
    }

    #endregion                  //----------------------------------------


    #region Public API          //----------------------------------------

    /// <summary>
    /// Activates this floating text element with the given display values.
    /// Called by Mb_PlayerFloatingTextManager when a combat event fires.
    ///
    /// anchorScreenPos  — screen-space position of the HUD anchor zone
    /// text             — the string to display (damage number, "SLOWED", etc.)
    /// color            — text color for this event type
    /// floatSpeed       — upward slide speed in pixels per second
    /// fadeDuration     — total animation duration in seconds
    /// horizontalOffset — random horizontal nudge to prevent perfect overlap
    /// fontSizeMultiplier — optional scale for emphasis (default 1.0)
    /// </summary>
    public void Activate(
        Vector2 anchorScreenPos,
        string text,
        Color color,
        float floatSpeed,
        float fadeDuration,
        float horizontalOffset,
        float fontSizeMultiplier = 1.0f)
    {
        if (_isAnimating)
        {
            // Safety net: if somehow activated while still running, stop the
            // old coroutine cleanly before starting fresh
            StopAllCoroutines();
            _isAnimating = false;
        }

        // Apply text and visual settings
        _label.text = text;
        _label.color = color;
        _label.fontSize *= fontSizeMultiplier;

        // Position at the anchor with the horizontal spread offset applied
        _rectTransform.position = new Vector3(
            anchorScreenPos.x + horizontalOffset,
            anchorScreenPos.y,
            0f
        );

        gameObject.SetActive(true);

        StartCoroutine(AnimateRoutine(floatSpeed, fadeDuration));
    }


    /// <summary>
    /// Immediately hides this element and resets it for pool reuse.
    /// Called by the manager if it needs to forcibly reclaim an element
    /// (e.g. during scene teardown).
    /// </summary>
    public void ForceRelease()
    {
        StopAllCoroutines();
        _isAnimating = false;
        ResetVisuals();
        gameObject.SetActive(false);
    }

    #endregion                  //----------------------------------------


    #region Animation           //----------------------------------------

    // Slides upward and fades alpha from 1 to 0 over fadeDuration seconds.
    // Uses unscaledDeltaTime so text keeps animating through pause — damage
    // numbers already in flight look wrong if they suddenly freeze.
    private IEnumerator AnimateRoutine(float floatSpeed, float fadeDuration)
    {
        _isAnimating = true;

        float elapsed = 0f;
        Color startColor = _label.color;
        Vector3 startPosition = _rectTransform.position;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            // t goes from 0 to 1 over the duration
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            // Slide upward — distance is speed × time, independent of fade progress
            _rectTransform.position = startPosition + Vector3.up * (floatSpeed * elapsed);

            // Fade alpha from fully opaque to fully transparent
            // Using a slight ease-in on fade (t²) so the text stays readable
            // longer at the start and vanishes quickly at the end
            float alpha = Mathf.Lerp(1f, 0f, t * t);
            _label.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            yield return null;
        }

        // Fully transparent and done — reset and signal the manager
        _isAnimating = false;
        ResetVisuals();
        gameObject.SetActive(false);

        // Notify the manager this element is ready to be reused
        OnReadyForPool?.Invoke(this);
    }

    #endregion                  //----------------------------------------


    #region Helpers             //----------------------------------------

    // Resets text, color, and scale to neutral defaults so a pool-reused
    // element never shows stale values from a previous activation.
    private void ResetVisuals()
    {
        if (_label == null) return;

        _label.text = string.Empty;
        _label.color = Color.white;

        // fontSize is modified by fontSizeMultiplier on Activate() —
        // we don't store the original, so we reset to a reasonable default.
        // TODO: Cache the prefab's default font size in Awake() and restore
        //       it here instead of hardcoding. This matters if the prefab's
        //       default font size changes during development.
        _label.fontSize = 36f;
    }

    #endregion                  //----------------------------------------
}