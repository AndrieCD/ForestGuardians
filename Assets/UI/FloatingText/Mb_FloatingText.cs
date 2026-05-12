// Mb_FloatingText.cs
// A single reusable floating text object in the FCT pool.
// Animates upward and fades out, then signals the pool to reclaim it.
// Uses a 3D TextMeshPro component — avoids Canvas overhead and works
// cleanly with billboard rotation in world space.
//
// Inspector Setup:
//   - Attach this to a prefab that has a TextMeshPro (3D) component on it.
//   - The prefab should have no Canvas. Add a MeshRenderer if TMP 3D requires one.
//   - Assign the prefab to Mb_FloatingTextPool in the Inspector.

using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class Mb_FloatingText : MonoBehaviour
{
    // The text renderer — 3D TMP, not UI TMP, so it lives in world space
    private TextMeshPro _tmp;

    // Cached camera reference — updated each frame for billboard rotation.
    // Cached in Awake so we're not calling Camera.main every frame (that's slow).
    private Camera _mainCamera;

    // Animation settings — configurable from the pool prefab or set via Initialize()
    [SerializeField] private float _floatSpeed = 0.5f;     // Units per second upward
    [SerializeField] private float _fadeDuration = 0.8f;   // How long the full fade takes

    // Called when the animation finishes so the pool knows to reclaim this object
    // Using Action instead of a static event keeps this instance-specific
    public event Action<Mb_FloatingText> OnAnimationComplete;

    // Whether this text is currently animating — prevents double-activation from pool
    private bool _isActive = false;


    private void Awake()
    {
        _tmp = GetComponent<TextMeshPro>();
        _mainCamera = Camera.main;

        if (_tmp == null)
            Debug.LogError("[Mb_FloatingText] Missing TextMeshPro component on prefab.");
    }


    // Called every frame while this object is active in the scene.
    // Only does billboard rotation — the float/fade happen in a coroutine.
    private void Update()
    {
        if (!_isActive) return;

        // Billboard: always face the camera so the text is readable from any angle.
        // We copy the camera's rotation directly rather than using LookAt so the
        // text doesn't flip when the camera is above or below the text.
        if (_mainCamera != null)
            transform.rotation = _mainCamera.transform.rotation;
    }


    /// <summary>
    /// Activates this floating text with the given content, color, and size.
    /// Called by Mb_FloatingTextPool when it hands out a pooled instance.
    /// </summary>
    /// <param name="worldPosition">Where to spawn the text (above the CuBot).</param>
    /// <param name="text">The string to display (e.g. "142" or "SLOW").</param>
    /// <param name="color">Text color — white for normal hits, yellow for crits, etc.</param>
    /// <param name="fontSize">Text size — damage numbers scale with hit size.</param>
    /// <param name="icon">Optional sprite to show before the text. Null = no icon.</param>
    public void Initialize(Vector3 worldPosition, string text, Color color, float fontSize, Sprite icon = null)
    {
        // Reset transform so pooled objects don't start at a weird position
        transform.position = worldPosition;

        _tmp.text = text;
        _tmp.color = color;
        _tmp.fontSize = fontSize;

        // TODO: Icon rendering via a secondary SpriteRenderer or a second TMP character
        // For now, if an icon sprite is provided, prefix the label with a placeholder symbol
        // until a proper icon rendering pass is implemented.
        // Suggested approach: place a SpriteRenderer as a child of this prefab, toggled on/off
        // based on whether icon != null. Position it to the left of the TMP pivot.
        if (icon != null)
        {
            // Placeholder: prepend a bullet so the layout visually reserves icon space
            // TODO: Replace with actual SpriteRenderer child showing icon sprite
            _tmp.text = "● " + text;
        }

        // Make sure alpha is fully opaque before starting the fade
        _tmp.alpha = 1f;
        _isActive = true;

        // Kick off the float + fade coroutine
        StartCoroutine(FloatAndFadeRoutine());
    }


    /// <summary>
    /// Moves the text upward and fades its alpha to zero, then returns it to the pool.
    /// Never calls Destroy() — pool reuse is the only reclaim method.
    /// </summary>
    private IEnumerator FloatAndFadeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;

            // Move upward at the configured speed
            transform.position += Vector3.up * _floatSpeed * Time.deltaTime;

            // Fade alpha from 1 to 0 over the full duration using linear interpolation
            _tmp.alpha = Mathf.Lerp(1f, 0f, elapsed / _fadeDuration);

            yield return null;
        }

        // Animation is done — signal the pool to take this object back
        _isActive = false;
        OnAnimationComplete?.Invoke(this);
    }


    /// <summary>
    /// Resets the object to a clean hidden state when returned to the pool.
    /// Called by Mb_FloatingTextPool before pooling this instance.
    /// </summary>
    public void ResetForPool()
    {
        _isActive = false;
        _tmp.text = "";
        _tmp.alpha = 0f;
        StopAllCoroutines(); // Safety: cancel any lingering animation coroutine
        gameObject.SetActive(false);
    }
}