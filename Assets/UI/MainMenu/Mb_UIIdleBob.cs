using UnityEngine;

/// <summary>
/// Adds subtle unscaled vertical motion to a UI RectTransform.
/// Presentation-only; it does not affect layout, input, or UI state.
/// </summary>
public class Mb_UIIdleBob : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _Amplitude = 6f;
    [SerializeField, Min(0.1f)] private float _CycleDuration = 6f;

    private RectTransform _rectTransform;
    private Vector2 _basePosition;
    private float _phase;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
        if (_rectTransform == null) return;

        _basePosition = _rectTransform.anchoredPosition;
        // Keep sibling foreground elements synchronized when a screen is composed
        // from multiple panels and decorative overlays.
        _phase = 0f;
    }

    private void OnDisable()
    {
        if (_rectTransform != null)
            _rectTransform.anchoredPosition = _basePosition;
    }

    private void Update()
    {
        if (_rectTransform == null) return;

        float angularSpeed = Mathf.PI * 2f / _CycleDuration;
        float offset = Mathf.Sin(Time.unscaledTime * angularSpeed + _phase) * _Amplitude;
        _rectTransform.anchoredPosition = _basePosition + Vector2.up * offset;
    }
}
