using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Provides inspector-driven hover scale feedback for a rewards card.
/// </summary>
public class Mb_RewardsCardUIAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Animation")]
    [Tooltip("Scale multiplier applied while the pointer is over the card.")]
    [SerializeField] private float HoverScaleMultiplier = 1.05f;

    [Tooltip("Seconds the card takes to reach its hover scale.")]
    [SerializeField] private float HoverDuration = 0.12f;

    [Tooltip("Ease used when entering and leaving the card hover state.")]
    [SerializeField] private Ease HoverEase = Ease.OutQuad;

    private Vector3 _baseScale;
    private Mb_UIAudioTrigger _audioTrigger;
    private Tween _hoverTween;

    private void Awake()
    {
        _baseScale = transform.localScale;
        _audioTrigger = GetComponent<Mb_UIAudioTrigger>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _audioTrigger?.OnHover();
        AnimateTo(_baseScale * Mathf.Max(0f, HoverScaleMultiplier));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTo(_baseScale);
    }

    public void StopAndReset()
    {
        _hoverTween?.Kill();
        _hoverTween = null;
        transform.localScale = _baseScale;
    }

    private void AnimateTo(Vector3 targetScale)
    {
        _hoverTween?.Kill();
        _hoverTween = transform
            .DOScale(targetScale, Mathf.Max(0f, HoverDuration))
            .SetEase(HoverEase)
            .SetUpdate(true);
    }

    private void OnDisable()
    {
        StopAndReset();
    }
}
