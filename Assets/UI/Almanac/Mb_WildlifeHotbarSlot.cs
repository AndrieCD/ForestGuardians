using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// Mb_WildlifeHotbarSlot
// Lives on the slot prefab root. Owns all child UI references for one slot.
// Kept in the same file since it only exists to serve Mb_WildlifeHotbarUI.
// ─────────────────────────────────────────────────────────────────────────────

public class Mb_WildlifeHotbarSlot : MonoBehaviour
{

    #region Inspector Fields            //----------------------------------------

    [Header("Slot UI References")]
    [Tooltip("Displays the species icon — silhouette while locked, full icon when complete.")]
    [SerializeField] private Image Icon;

    [Tooltip("Displays '???' while locked, shows CommonName on completion.")]
    [SerializeField] private TMP_Text SpeciesNameText;

    [Tooltip("Displays found/required count e.g. '0 / 2'.")]
    [SerializeField] private TMP_Text CounterText;

    [Tooltip("GameObject shown only when the species is fully collected. " +
             "Can be a checkmark, highlight border, or completion glow.")]
    [SerializeField] private GameObject CompletionOverlay;

    [Header("Icon Tint")]
    [SerializeField] private Color LockedIconTint = Color.white;
    [SerializeField] private Color UnlockedIconTint = Color.white;

    [Header("Discovery Feedback")]
    [SerializeField] private float FeedbackDuration = 1.25f;
    [SerializeField] private float FeedbackPulseSpeed = 8f;
    [SerializeField] private float FeedbackMinimumAlpha = 0.35f;
    [SerializeField] private float FeedbackScale = 1.12f;

    #endregion                          //----------------------------------------


    #region Runtime State               //----------------------------------------

    private Coroutine _feedbackCoroutine;
    private Color _feedbackBaseColor;
    private Vector3 _feedbackBaseScale = Vector3.one;
    private bool _hasFeedbackBaseState;

    #endregion                          //----------------------------------------


    #region Unity Lifecycle             //----------------------------------------

    private void OnDisable()
    {
        StopDiscoveryFeedback();
    }

    private void OnValidate()
    {
        FeedbackDuration = Mathf.Max(0f, FeedbackDuration);
        FeedbackPulseSpeed = Mathf.Max(0.01f, FeedbackPulseSpeed);
        FeedbackMinimumAlpha = Mathf.Clamp01(FeedbackMinimumAlpha);
        FeedbackScale = Mathf.Max(1f, FeedbackScale);
    }

    #endregion                          //----------------------------------------


    #region Slot State API              //----------------------------------------

    /// <summary>
    /// Sets the slot to its initial locked state:
    ///   - Silhouette icon
    ///   - "???" species name
    ///   - "0 / N" counter
    ///   - Completion overlay hidden
    /// </summary>
    public void InitializeLocked(SO_WildlifeEntry entry)
    {
        StopDiscoveryFeedback();

        // Show silhouette while the species is not yet unlocked
        if (Icon != null)
        {
            Icon.sprite = entry.SilhouetteIcon;
            Icon.gameObject.SetActive(entry.SilhouetteIcon != null);
            Icon.color = LockedIconTint;
            Icon.rectTransform.localScale = Vector3.one;
        }

        if (SpeciesNameText != null)
            SpeciesNameText.text = "???";

        if (CounterText != null)
            CounterText.text = $"0 / {entry.RequiredCount}";

        if (CompletionOverlay != null)
            CompletionOverlay.SetActive(false);
    }


    /// <summary>
    /// Updates the found/required counter text.
    /// Called by Mb_WildlifeHotbarUI on OnSpeciesProgress.
    /// </summary>
    public void UpdateCounter(int found, int required)
    {
        if (CounterText != null)
            CounterText.text = $"{found} / {required}";
    }


    /// <summary>
    /// Transitions the slot to its completed state:
    ///   - Full species icon (unlocked)
    ///   - Shows CommonName
    ///   - Counter shows N / N
    ///   - Completion overlay shown
    /// </summary>
    public void MarkComplete(SO_WildlifeEntry entry)
    {
        StopDiscoveryFeedback();

        if (Icon != null)
        {
            // Swap to the full unlocked icon on completion
            Icon.sprite = entry.UnlockedIcon != null
                ? entry.UnlockedIcon
                : entry.SilhouetteIcon; // Fallback if unlocked icon not yet assigned

            Icon.color = UnlockedIconTint;
            Icon.rectTransform.localScale = Vector3.one;

            Icon.gameObject.SetActive(true);
        }

        if (SpeciesNameText != null)
            SpeciesNameText.text = entry.CommonName;

        if (CounterText != null)
            CounterText.text = $"{entry.RequiredCount} / {entry.RequiredCount}";

        if (CompletionOverlay != null)
            CompletionOverlay.SetActive(true);
    }

    #endregion                          //----------------------------------------


    #region Discovery Feedback          //----------------------------------------

    public void PlayDiscoveryFeedback()
    {
        if (Icon == null || !gameObject.activeInHierarchy)
            return;

        StopDiscoveryFeedback();

        _feedbackBaseColor = Icon.color;
        _feedbackBaseScale = Icon.rectTransform.localScale;
        _hasFeedbackBaseState = true;
        _feedbackCoroutine = StartCoroutine(DiscoveryFeedbackRoutine());
    }

    private IEnumerator DiscoveryFeedbackRoutine()
    {
        float elapsed = 0f;
        Color fadedColor = _feedbackBaseColor;
        fadedColor.a = Mathf.Min(_feedbackBaseColor.a, FeedbackMinimumAlpha);
        Vector3 targetScale = _feedbackBaseScale * FeedbackScale;

        while (elapsed < FeedbackDuration)
        {
            float pulse = Mathf.PingPong(elapsed * FeedbackPulseSpeed, 1f);
            Icon.color = Color.Lerp(_feedbackBaseColor, fadedColor, pulse);
            Icon.rectTransform.localScale = Vector3.Lerp(_feedbackBaseScale, targetScale, pulse);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        RestoreFeedbackBaseState();
        _feedbackCoroutine = null;
    }

    private void StopDiscoveryFeedback()
    {
        if (_feedbackCoroutine != null)
        {
            StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = null;
        }

        RestoreFeedbackBaseState();
    }

    private void RestoreFeedbackBaseState()
    {
        if (!_hasFeedbackBaseState || Icon == null)
            return;

        Icon.color = _feedbackBaseColor;
        Icon.rectTransform.localScale = _feedbackBaseScale;
        _hasFeedbackBaseState = false;
    }

    #endregion                          //----------------------------------------
}
