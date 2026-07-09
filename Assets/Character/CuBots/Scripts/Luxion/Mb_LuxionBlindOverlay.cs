using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Boss-specific white flash overlay for Luxion's Flash Photography.
/// Attach this to a HUD object with a full-screen white Image and CanvasGroup.
/// </summary>
public class Mb_LuxionBlindOverlay : MonoBehaviour
{
    public static event Action<float> OnBlindRequested;

    [SerializeField] private CanvasGroup _OverlayGroup;
    [SerializeField] private float _FadeInDuration = 0.08f;
    [SerializeField] private float _FadeOutDuration = 0.35f;

    private Coroutine _blindRoutine;

    public static void RequestBlind(float duration)
    {
        OnBlindRequested?.Invoke(duration);
    }

    private void Awake()
    {
        if (_OverlayGroup == null)
            _OverlayGroup = GetComponent<CanvasGroup>();

        if (_OverlayGroup != null)
            _OverlayGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        OnBlindRequested += HandleBlindRequested;
    }

    private void OnDisable()
    {
        OnBlindRequested -= HandleBlindRequested;
    }

    private void HandleBlindRequested(float duration)
    {
        if (_OverlayGroup == null) return;

        if (_blindRoutine != null)
            StopCoroutine(_blindRoutine);

        _blindRoutine = StartCoroutine(BlindRoutine(Mathf.Max(0f, duration)));
    }

    private IEnumerator BlindRoutine(float duration)
    {
        yield return FadeTo(1f, _FadeInDuration);
        yield return new WaitForSeconds(duration);
        yield return FadeTo(0f, _FadeOutDuration);

        _blindRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = _OverlayGroup.alpha;

        if (duration <= 0f)
        {
            _OverlayGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _OverlayGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        _OverlayGroup.alpha = targetAlpha;
    }
}
