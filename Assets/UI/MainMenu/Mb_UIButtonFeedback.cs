using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Provides subtle hover and press scale feedback for an application UI control.
/// Owns only presentation state; it does not change Button or Toggle callbacks.
/// </summary>
public class Mb_UIButtonFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Scale Feedback")]
    [SerializeField, Range(1f, 1.1f)] private float _HoverScale = 1.04f;
    [SerializeField, Range(0.9f, 1f)] private float _PressedScale = 0.96f;
    [SerializeField, Min(0.01f)] private float _TransitionSpeed = 14f;

    private Vector3 _baseScale;
    private bool _isHovered;
    private bool _isPressed;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (_baseScale == Vector3.zero)
            _baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        _isHovered = false;
        _isPressed = false;
        transform.localScale = _baseScale;
    }

    private void Update()
    {
        float targetMultiplier = _isPressed
            ? _PressedScale
            : (_isHovered ? _HoverScale : 1f);

        Vector3 targetScale = _baseScale * targetMultiplier;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            1f - Mathf.Exp(-_TransitionSpeed * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(PointerEventData eventData) => _isHovered = true;

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) => _isPressed = true;

    public void OnPointerUp(PointerEventData eventData) => _isPressed = false;
}
