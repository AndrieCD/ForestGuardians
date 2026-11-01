using System.Collections;
using UnityEngine;

/// <summary>
/// Performs a short scale-in when an application UI screen opens.
/// The component owns only presentation state and leaves navigation to its caller.
/// </summary>
public class Mb_UIScreenTransition : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _Duration = 0.18f;
    [SerializeField, Range(0.9f, 1f)] private float _StartingScale = 0.96f;

    private CanvasGroup _canvasGroup;
    private Vector3 _baseScale;
    private float _baseAlpha;
    private Coroutine _transition;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _baseScale = transform.localScale;
        _baseAlpha = _canvasGroup.alpha;
    }

    public void PlayEnter()
    {
        if (!isActiveAndEnabled) return;

        if (_transition != null)
            StopCoroutine(_transition);

        _transition = StartCoroutine(EnterRoutine());
    }

    private IEnumerator EnterRoutine()
    {
        // Keep the canvas visible so shared backgrounds do not flash to black
        // while the new screen content settles into place.
        _canvasGroup.alpha = _baseAlpha;
        transform.localScale = _baseScale * _StartingScale;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < _Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / _Duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            transform.localScale = Vector3.Lerp(
                _baseScale * _StartingScale,
                _baseScale,
                eased);

            yield return null;
        }

        _canvasGroup.alpha = _baseAlpha;
        transform.localScale = _baseScale;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        _transition = null;
    }
}
