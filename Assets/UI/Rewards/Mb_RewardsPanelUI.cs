// Mb_RewardsPanelUI.cs
// Drives the Rewards Panel Canvas — the UI the player sees when choosing a reward.
//
// SELECTION FEEDBACK FLOW (same for both manual and auto-select):
//   1. Mb_RewardsManager calls ShowSelectionFeedback(side) after applying the reward
//   2. Unchosen card is immediately hidden (SetActive false)
//   3. Timer label is immediately hidden
//   4. Chosen card stays visible for CardHoldDuration seconds
//   5. Entire panel fades out over FadeOutDuration seconds
//   6. OnPanelFadeComplete fires — Mb_RewardsManager handles state cleanup there
//   7. Panel deactivates
//
// CLOSING RESPONSIBILITY:
//   Mb_RewardsPanelUI owns all visual closing — including fade and deactivation.
//   Mb_RewardsManager never calls Hide() during normal gameplay. It only calls
//   Hide() as a force-close for edge cases like scene teardown.
//
// LAYOUT (set up in the Inspector / Unity Editor):
//   Rewards_Canvas (this GameObject — has CanvasGroup)
//   ├── Overlay
//   ├── TimerText      (TMP_Text — countdown label)
//   ├── LeftCard       (Button)
//   │   ├── Icon       (Image)
//   │   ├── Name       (TMP_Text)
//   │   └── Description(TMP_Text)
//   └── RightCard      (Button)
//       ├── Icon       (Image)
//       ├── Name       (TMP_Text)
//       └── Description(TMP_Text)
//
// Inspector Setup:
//   - Assign all card fields, timer text, and the RewardsManager reference.
//   - CardHoldDuration: how long the chosen card stays before fade (default 1s).
//   - FadeOutDuration: how long the full panel fade takes (default 0.3s).
//   - CanvasGroup is fetched automatically in Awake — no need to assign it.

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum RewardPanelSide { Left, Right }

public class Mb_RewardsPanelUI : MonoBehaviour
{
    #region Events                  //----------------------------------------

    // Fired at the end of the fade-out — Mb_RewardsManager listens to this
    // to handle game state cleanup after the animation finishes.
    public event Action OnPanelFadeComplete;

    #endregion                      //----------------------------------------


    #region Inspector Fields        //----------------------------------------

    [Header("Left Card")]
    [SerializeField] private Button _LeftCard;
    [SerializeField] private Image _LeftIcon;
    [SerializeField] private TMP_Text _LeftName;
    [SerializeField] private TMP_Text _LeftDescription;

    [Header("Right Card")]
    [SerializeField] private Button _RightCard;
    [SerializeField] private Image _RightIcon;
    [SerializeField] private TMP_Text _RightName;
    [SerializeField] private TMP_Text _RightDescription;

    [Header("Timer")]
    [Tooltip("TMP_Text label showing the rewards countdown. Hidden when a card is chosen.")]
    [SerializeField] private TMP_Text _TimerText;

    [Header("References")]
    [SerializeField] private Mb_RewardsManager _RewardsManager;

    [Header("Selection Feedback")]
    [Tooltip("Seconds the chosen card stays visible before the panel fades out.")]
    [SerializeField] private float CardHoldDuration = 1f;

    [Tooltip("Seconds the full panel takes to fade to transparent before deactivating.")]
    [SerializeField] private float FadeOutDuration = 0.3f;

    #endregion                      //----------------------------------------


    #region Private State           //----------------------------------------

    private CanvasGroup _canvasGroup;
    private Coroutine _feedbackCoroutine;

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        if (_canvasGroup == null)
            Debug.LogError("[Mb_RewardsPanelUI] No CanvasGroup found on this GameObject. " +
                           "Add one to Rewards_Canvas in the Inspector.");
    }


    private void OnEnable()
    {
        // Reset alpha every time the panel opens — a previous fade may have
        // left it at a non-1 value
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        SetCardsInteractable(true);

        Mb_RewardsManager.OnRewardsTimerTick += HandleTimerTick;
    }


    private void OnDisable()
    {
        Mb_RewardsManager.OnRewardsTimerTick -= HandleTimerTick;
    }

    #endregion                      //----------------------------------------


    #region Public API              //----------------------------------------

    /// <summary>
    /// Called by Mb_RewardsManager to populate and show the panel.
    /// </summary>
    public void Show(RewardOption left, RewardOption right)
    {


        // Ensure both cards are visible — a previous selection may have hidden one
        _LeftCard.gameObject.SetActive(true);
        _RightCard.gameObject.SetActive(true);

        PopulateCard(_LeftIcon, _LeftName, _LeftDescription, left);
        PopulateCard(_RightIcon, _RightName, _RightDescription, right);


        if (_TimerText != null)
            _TimerText.gameObject.SetActive(true);

        gameObject.SetActive(true);

        StartCoroutine(EnableInteractionAfterDelay(left, right));

    }

    IEnumerator EnableInteractionAfterDelay(RewardOption left, RewardOption right)
    {
        // 0.5s delay before allowing interaction to avoid accidental clicks during panel open animation
        yield return new WaitForSeconds(1.5f);

        _LeftCard.interactable = !left.IsMaxedPlaceholder;
        _RightCard.interactable = !right.IsMaxedPlaceholder;
    }


    /// <summary>
    /// Triggers the selection feedback animation for the given side.
    /// Called by Mb_RewardsManager after the reward has been applied —
    /// works identically for both manual clicks and auto-select.
    /// OnPanelFadeComplete fires when the animation finishes.
    /// </summary>
    public void ShowSelectionFeedback(RewardPanelSide chosenSide)
    {
        GameObject chosenCard = chosenSide == RewardPanelSide.Left
            ? _LeftCard.gameObject
            : _RightCard.gameObject;

        GameObject hiddenCard = chosenSide == RewardPanelSide.Left
            ? _RightCard.gameObject
            : _LeftCard.gameObject;

        // Stop any in-flight feedback coroutine before starting a new one
        if (_feedbackCoroutine != null)
            StopCoroutine(_feedbackCoroutine);

        _feedbackCoroutine = StartCoroutine(FeedbackRoutine(chosenCard, hiddenCard));
    }


    /// <summary>
    /// Force-closes the panel instantly with no animation.
    /// Use only for edge cases like scene teardown or skipped rewards.
    /// For normal gameplay closing, Mb_RewardsManager calls ShowSelectionFeedback().
    /// </summary>
    public void Hide()
    {
        if (_feedbackCoroutine != null)
        {
            StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = null;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        gameObject.SetActive(false);
    }

    #endregion                      //----------------------------------------


    #region Button Callbacks        //----------------------------------------

    public void OnLeftCardClicked()
    {
        SetCardsInteractable(false);
        _RewardsManager.OnLeftChosen();
    }

    public void OnRightCardClicked()
    {
        SetCardsInteractable(false);
        _RewardsManager.OnRightChosen();
    }

    #endregion                      //----------------------------------------


    #region Feedback Animation      //----------------------------------------

    private IEnumerator FeedbackRoutine(GameObject chosenCard, GameObject hiddenCard)
    {
        // Step 1: Instantly hide the unchosen card and timer
        hiddenCard.SetActive(false);

        if (_TimerText != null)
            _TimerText.gameObject.SetActive(false);


        // Scale up then scale down the selected card
        if (chosenCard != null)
        {
            Vector3 originalScale = chosenCard.transform.localScale;
            Vector3 targetScale = originalScale * 1.1f; // Scale up by 10%
            float elapsed = 0f;
            float scaleDuration = 0.2f; // Total time for the scale animation
            // Scale up
            while (elapsed < scaleDuration / 2f)
            {
                elapsed += Time.unscaledDeltaTime;
                chosenCard.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / (scaleDuration / 2f));
                yield return null;
            }
            // Ensure it reaches the exact target scale
            chosenCard.transform.localScale = targetScale;
            // Reset elapsed for scale down
            elapsed = 0f;
            // Scale down
            while (elapsed < scaleDuration / 2f)
            {
                elapsed += Time.unscaledDeltaTime;
                chosenCard.transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / (scaleDuration / 2f));
                yield return null;
            }
            // Ensure it resets to the original scale
            chosenCard.transform.localScale = originalScale;
        }


        // Step 2: Hold — only the chosen card is visible
        yield return new WaitForSeconds(CardHoldDuration);

        // Step 3: Fade the entire panel out
        yield return StartCoroutine(FadeOutRoutine());

        // Step 4: Notify RewardsManager that the animation is done —
        // it handles game state and event cleanup from here
        OnPanelFadeComplete?.Invoke();

        gameObject.SetActive(false);
        _feedbackCoroutine = null;
    }


    private IEnumerator FadeOutRoutine()
    {
        float elapsed = 0f;

        while (elapsed < FadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / FadeOutDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }

    #endregion                      //----------------------------------------


    #region Timer Display           //----------------------------------------

    private void HandleTimerTick(float remaining)
    {
        if (_TimerText == null) return;
        _TimerText.text = $"CHOOSING IN {Mathf.CeilToInt(remaining)}";
    }

    #endregion                      //----------------------------------------


    #region Helpers                 //----------------------------------------

    private void PopulateCard(Image iconImg, TMP_Text nameText, TMP_Text descText, RewardOption option)
    {
        nameText.text = option.Name;
        descText.text = option.Description;

        if (option.Icon != null)
        {
            iconImg.sprite = option.Icon;
            iconImg.gameObject.SetActive(true);
        }
        else
        {
            iconImg.gameObject.SetActive(false);
        }
    }


    private void SetCardsInteractable(bool state)
    {
        var buttons = GetComponentsInChildren<Button>();
        foreach (var btn in buttons)
            btn.interactable = state;
    }

    #endregion                      //----------------------------------------
}