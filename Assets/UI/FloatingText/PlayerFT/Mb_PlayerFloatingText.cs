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
//   - Pause-safe: uses Time.unscaledDeltaTime so the animation doesn't
//     freeze when timeScale = 0.
//
// Inspector setup:
//   - Prefab needs a RectTransform and a TMP_Text on the same GameObject.
//   - No Inspector fields to assign — everything is passed via Activate().

using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class Mb_PlayerFloatingText : MonoBehaviour
{
    #region References          //----------------------------------------

    private TMP_Text _label;
    private RectTransform _rectTransform;

    #endregion                  //----------------------------------------


    #region State               //----------------------------------------

    public event Action<Mb_PlayerFloatingText> OnReadyForPool;

    private bool _isAnimating = false;

    // Cached at Awake so ResetVisuals() always restores the correct prefab default,
    // regardless of what fontSizeMultiplier did to it during a previous activation.
    // This was the root cause of font size drift across pool reuses.
    private float _defaultFontSize;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        _label = GetComponent<TMP_Text>();
        _rectTransform = GetComponent<RectTransform>();

        if (_label == null)
        {
            Debug.LogError($"[Mb_PlayerFloatingText] No TMP_Text found on {gameObject.name}.");
            return;
        }

        // Cache the font size set on the prefab so we can restore it exactly on reset.
        // Using the prefab's value rather than a hardcoded constant means the designer
        // can change the font size in the prefab without touching code.
        _defaultFontSize = _label.fontSize;
    }

    #endregion                  //----------------------------------------


    #region Public API          //----------------------------------------

    /// <summary>
    /// Activates this floating text element with the given display values.
    /// Called by Mb_PlayerFloatingTextManager when a combat event fires.
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
        // Stop any in-flight animation before starting fresh — prevents
        // the coroutine from firing OnReadyForPool twice for one element
        if (_isAnimating)
        {
            StopAllCoroutines();
            _isAnimating = false;
        }

        // Always reset visuals first so we start from a clean known state,
        // not whatever the previous activation left behind
        ResetVisuals();

        _label.text = text;
        _label.color = color;

        // Apply multiplier against the CACHED default, not the current value.
        // The old code did _label.fontSize *= multiplier which would compound
        // across reuses if ResetVisuals() was ever skipped.
        _label.fontSize = _defaultFontSize * fontSizeMultiplier;

        _rectTransform.position = new Vector3(
            anchorScreenPos.x + horizontalOffset,
            anchorScreenPos.y,
            0f
        );

        gameObject.SetActive(true);
        StartCoroutine(AnimateRoutine(floatSpeed, fadeDuration));
    }


    /// <summary>
    /// Immediately stops animation, resets visuals, and deactivates this element.
    /// Called by the manager to force all in-flight elements back to the pool
    /// on wave transitions — prevents elements from getting stuck on screen.
    /// </summary>
    public void ForceRelease()
    {
        if (_isAnimating)
        {
            StopAllCoroutines();
            _isAnimating = false;
        }

        ResetVisuals();
        gameObject.SetActive(false);

        // Notify the manager so this element is returned to the pool.
        // The manager calls ForceRelease() precisely because it wants the element
        // back — firing the event here means the manager doesn't need a separate
        // code path to re-enqueue it.
        OnReadyForPool?.Invoke(this);
    }

    #endregion                  //----------------------------------------


    #region Animation           //----------------------------------------

    private IEnumerator AnimateRoutine(float floatSpeed, float fadeDuration)
    {
        _isAnimating = true;

        float elapsed = 0f;
        Color startColor = _label.color;
        Vector3 startPosition = _rectTransform.position;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / fadeDuration);

            _rectTransform.position = startPosition + Vector3.up * (floatSpeed * elapsed);

            // Ease-in on the fade (t²) keeps the text readable longer at the
            // start and makes it vanish quickly at the end
            float alpha = Mathf.Lerp(1f, 0f, t * t);
            _label.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            yield return null;
        }

        _isAnimating = false;
        ResetVisuals();
        gameObject.SetActive(false);

        OnReadyForPool?.Invoke(this);
    }

    #endregion                  //----------------------------------------


    #region Helpers             //----------------------------------------

    private void ResetVisuals()
    {
        if (_label == null) return;

        _label.text = string.Empty;
        _label.color = Color.white;

        // Restore the exact font size from the prefab — not a hardcoded constant.
        // _defaultFontSize is set once in Awake() from the prefab's configured value.
        _label.fontSize = _defaultFontSize;
    }

    #endregion                  //----------------------------------------
}