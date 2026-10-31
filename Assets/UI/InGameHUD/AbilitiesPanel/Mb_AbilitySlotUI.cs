// Mb_AbilitySlotUI.cs
// Drives one ability slot on the Abilities Panel.
// Separated from Mb_AbilitiesPanelUI so each slot is self-contained —
// the panel just initializes each slot and routes events to it.
//
// HOW IT WORKS:
//   - Initialize() sets up the icon, pips, and overlay for a known ability.
//   - ShowLocked() shows a locked/empty state when the ability is null (R pre-branch).
//   - HandleCooldownChanged() is subscribed directly by Mb_AbilitiesPanelUI.
//   - HandleLevelChanged() refreshes pips when the ability levels up.
//   - Cooldown overlay fill is updated in Update() only while _isOnCooldown is true.
//   - Ready state: optional glow and subtle icon pulse.
//   - Cooldown start: dimmed icon, dimmed pips, and a brief activation flash.
//   - Cooldown end: one-time icon pop and glow notification before returning
//     to the normal ready state.
//
// HIERARCHY (one per slot GO):
//   SlotRoot (Mb_AbilitySlotUI on this GO)
//   ├── SlotIcon          (Image — the ability icon)
//   ├── CooldownOverlay   (Image — fillMethod Vertical, fillOrigin Bottom)
//   └── PipsContainer     (any layout group — holds pip Image children)
//
// Inspector Setup:
//   - SlotIcon: the ability icon Image.
//   - CooldownOverlay: the overlay Image.
//   - PipsContainer: the Transform that holds pip child Images.
//     Pip Images are spawned dynamically at Initialize() time — no manual setup needed.

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

public class Mb_AbilitySlotUI : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [SerializeField] private Image SlotIcon;
    [SerializeField] private Image CooldownOverlay;

    [Header("State Visuals")]
    [Tooltip("Optional image behind the ability icon. It is shown while the ability is ready.")]
    [SerializeField] private Image ReadyGlow;

    [Tooltip("Optional image shown behind SlotIcon while the ultimate is not selected.")]
    [SerializeField] private Image DefaultUltimateIcon;

    [Tooltip("Parent Transform that will hold the pip Image children. " +
             "Add a Horizontal Layout Group component to this GO for automatic spacing.")]
    [SerializeField] private Transform PipsContainer;

    // TODO: Tune pip size — set Width/Height on the pip prefab or via Layout Element.
    // A good starting size is 8x8 px with 2px spacing in the Layout Group.
    [Tooltip("Prefab for a single pip Image. Must have an Image component.")]
    [SerializeField] private GameObject PipPrefab;

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    // Sprites for filled/unfilled pips — set by Initialize()
    private Sprite _pipFilledSprite;
    private Sprite _pipEmptySprite;

    // Live pip Image references — rebuilt when MaxLevel changes (future-proof)
    private List<Image> _pipImages = new List<Image>();

    // Opacity values — set by Initialize() from panel-level Inspector values
    private float _defaultOpacity;
    private float _cooldownOpacity;
    private float _activeOpacity;
    private float _readyPulseScale = 1.12f;
    private float _readyPulseDuration = 0.65f;
    private Vector3 _baseIconScale = Vector3.one;
    private Color _baseIconColor = Color.white;

    private Color _cooldownIconTint = new Color(0.58f, 0.58f, 0.58f, 1f);
    private Color _cooldownPipTint = new Color(0.60f, 0.60f, 0.60f, 1f);
    private Color _readyGlowColor = new Color(0.95f, 0.68f, 0.15f, 1f);

    // Cooldown tracking
    private bool _isOnCooldown = false;
    private float _cooldownTotal = 0f;
    private float _cooldownRemaining = 0f;
    private float _prevCooldownValue = 0f;

    // DOTween handles for state transitions and feedback.
    private Tween _readyTween;
    private Tween _activationTween;
    private Tween _readyNotificationTween;
    private Tween _glowTween;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Update()
    {
        // Overlay fill — only runs while on cooldown (bool guard)
        if (!_isOnCooldown || CooldownOverlay == null || _cooldownTotal <= 0f) return;

        CooldownOverlay.fillAmount = Mathf.Clamp01(_cooldownRemaining / _cooldownTotal);
    }

    private void OnDisable()
    {
        StopAllVisualTweens();
    }

    #endregion                  //----------------------------------------


    #region Public API          //----------------------------------------

    /// <summary>
    /// Sets up this slot for a known, non-null ability.
    /// Called by Mb_AbilitiesPanelUI.BindSlot() after fetching the ability reference.
    /// </summary>
    public void Initialize(
    Sprite icon,
    int currentLevel,
    int maxLevel,
    Sprite pipFilled,
    Sprite pipEmpty,
    float defaultOpacity,
    float cooldownOpacity,
    float activeOpacity,
    Color overlayColor,
    float readyPulseScale,
    float readyPulseDuration,
    Color cooldownIconTint,
    Color cooldownPipTint,
    Color readyGlowColor,
    bool showPips = true)   // ← add this
    {
        StopAllVisualTweens();

        _pipFilledSprite = pipFilled;
        _pipEmptySprite = pipEmpty;
        _defaultOpacity = defaultOpacity;
        _cooldownOpacity = cooldownOpacity;
        _activeOpacity = activeOpacity;
        _readyPulseScale = Mathf.Max(1f, readyPulseScale);
        _readyPulseDuration = Mathf.Max(0.01f, readyPulseDuration);
        _cooldownIconTint = cooldownIconTint;
        _cooldownPipTint = cooldownPipTint;
        _readyGlowColor = readyGlowColor;

        if (SlotIcon != null)
        {
            _baseIconScale = SlotIcon.rectTransform.localScale;
            _baseIconColor = SlotIcon.color;
            SlotIcon.sprite = icon;
            SlotIcon.enabled = icon != null;
            SetIconColor(_baseIconColor, _defaultOpacity);
            SetIconScale(_baseIconScale);
        }

        if (DefaultUltimateIcon != null)
            DefaultUltimateIcon.enabled = false;

        if (CooldownOverlay != null)
        {
            CooldownOverlay.color = overlayColor;
            CooldownOverlay.fillAmount = 0f;
        }

        // Only build pips for slots that support leveling (Q and E)
        if (showPips)
        {
            BuildPips(maxLevel);
            RefreshPips(currentLevel, maxLevel);
        }
        else
        {
            // Hide pips container entirely for R slot
            if (PipsContainer != null)
                PipsContainer.gameObject.SetActive(false);
        }

        _isOnCooldown = false;
        _cooldownTotal = 0f;
        _cooldownRemaining = 0f;
        _prevCooldownValue = 0f;
        ApplyReadyVisuals(false);
    }


    /// <summary>
    /// Shows the locked/empty state — used for R slot before branch is chosen.
    /// Hides pips and clears the overlay.
    /// </summary>
    public void ShowLocked(Sprite lockedIcon, Color overlayColor)
    {
        StopAllVisualTweens();

        if (SlotIcon != null)
        {
            _baseIconScale = SlotIcon.rectTransform.localScale;
            SlotIcon.sprite = lockedIcon;
            SlotIcon.enabled = false;

            // Locked state uses a dimmed opacity so it reads as unavailable
            // TODO: Tune this value — 0.4 is visibly greyed out without being invisible.
            SetIconColor(_baseIconColor, 0.4f);
            SetIconScale(_baseIconScale);
        }

        if (DefaultUltimateIcon != null)
            DefaultUltimateIcon.enabled = true;

        if (CooldownOverlay != null)
        {
            CooldownOverlay.color = overlayColor;
            CooldownOverlay.fillAmount = 0f;
        }

        // Clear pips — no level to display for a locked slot
        ClearPips();

        _isOnCooldown = false;
    }


    /// <summary>
    /// Subscribed to Sc_BaseAbility.OnCooldownChanged by Mb_AbilitiesPanelUI.
    /// Detects cooldown start (remaining jumped up), tracks remaining, detects end.
    /// </summary>
    public void HandleCooldownChanged(float remaining)
    {
        float clampedRemaining = Mathf.Max(0f, remaining);
        _cooldownRemaining = clampedRemaining;

        // Detect cooldown start — same pattern as Mb_ReticleUI
        if (clampedRemaining > 0f && clampedRemaining > _prevCooldownValue + 0.05f)
        {
            _cooldownTotal = clampedRemaining;
            _isOnCooldown = true;

            ApplyCooldownVisuals();
            PlayActivationFeedback();
        }

        // Cooldown finished
        if (clampedRemaining <= 0f && _isOnCooldown)
        {
            _isOnCooldown = false;
            if (CooldownOverlay != null)
                CooldownOverlay.fillAmount = 0f;

            ApplyReadyVisuals(true);
        }

        _prevCooldownValue = clampedRemaining;
    }


    /// <summary>
    /// Subscribed to Sc_BaseAbility.OnAbilityLevelChanged by Mb_AbilitiesPanelUI.
    /// Only swaps pip sprites — never destroys or rebuilds the pip row.
    /// </summary>
    public void HandleLevelChanged(int newLevel)
    {
        // Guard: if pip count doesn't match what we expect, something went wrong
        // at Initialize() time. Log it so it's easy to catch during testing.
        if (_pipImages.Count == 0)
        {
            Debug.LogWarning("[Mb_AbilitySlotUI] HandleLevelChanged called but no pips exist. " +
                             "Was Initialize() called before this ability leveled up?");
            return;
        }

        RefreshPips(newLevel, _pipImages.Count);
    }

    #endregion                  //----------------------------------------


    #region Pip System          //----------------------------------------

    // Destroys any existing pips and spawns a fresh row of maxLevel pips.
    // Called once per Initialize() — rebuilds only if maxLevel changes.
    private void BuildPips(int maxLevel)
    {
        ClearPips();

        if (PipsContainer == null || PipPrefab == null) return;

        for (int i = 0; i < maxLevel; i++)
        {
            GameObject pipGO = Instantiate(PipPrefab, PipsContainer);
            Image pipImage = pipGO.GetComponent<Image>();

            if (pipImage == null)
            {
                Debug.LogWarning("[Mb_AbilitySlotUI] PipPrefab has no Image component.");
                continue;
            }

            _pipImages.Add(pipImage);
        }
    }


    // Updates each pip sprite to filled or empty based on current level.
    private void RefreshPips(int currentLevel, int maxLevel)
    {
        for (int i = 0; i < _pipImages.Count; i++)
        {
            // Skip destroyed or missing pips rather than throwing a NullRef
            if (_pipImages[i] == null) continue;

            bool isFilled = (i < currentLevel);

            // Only assign if the sprite actually needs to change —
            // avoids marking the Image dirty every level-up call
            Sprite target = isFilled ? _pipFilledSprite : _pipEmptySprite;
            if (_pipImages[i].sprite != target)
                _pipImages[i].sprite = target;
        }
    }


    private void ClearPips()
    {
        foreach (var pip in _pipImages)
        {
            if (pip != null)
                Destroy(pip.gameObject);
        }
        _pipImages.Clear();
    }

    #endregion                  //----------------------------------------


    #region Animation           //----------------------------------------

    private void ApplyReadyVisuals(bool playNotification)
    {
        StopCooldownTweens();

        if (SlotIcon != null && SlotIcon.enabled)
        {
            SetIconColor(_baseIconColor, _defaultOpacity);
            SetIconScale(_baseIconScale);
        }

        SetPipTint(Color.white);

        if (ReadyGlow != null)
        {
            StartReadyGlow();
        }

        if (playNotification)
            PlayCooldownCompleteFeedback();
        else
            StartReadyPulse();
    }


    private void ApplyCooldownVisuals()
    {
        StopReadyTweens();

        if (SlotIcon != null && SlotIcon.enabled)
        {
            Color cooldownColor = _cooldownIconTint;
            cooldownColor.a = _cooldownOpacity;
            SlotIcon.color = cooldownColor;
            SetIconScale(_baseIconScale);
        }

        SetPipTint(_cooldownPipTint);

        if (ReadyGlow != null)
        {
            _glowTween?.Kill();
            _glowTween = ReadyGlow.DOFade(0f, 0.12f).SetUpdate(true);
        }
    }


    private void StartReadyPulse()
    {
        _readyTween?.Kill();

        if (SlotIcon == null || !SlotIcon.enabled)
            return;

        _readyTween = SlotIcon.rectTransform
            .DOScale(_baseIconScale * _readyPulseScale, _readyPulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }


    private void PlayActivationFeedback()
    {
        _activationTween?.Kill();

        if (SlotIcon == null || !SlotIcon.enabled)
            return;

        _activationTween = DOTween.Sequence()
            .Append(SlotIcon.DOFade(_activeOpacity, 0.08f))
            .Append(SlotIcon.DOFade(_cooldownOpacity, 0.12f))
            .SetUpdate(true);
    }


    private void PlayCooldownCompleteFeedback()
    {
        _readyNotificationTween?.Kill();
        _readyTween?.Kill();

        PlayReadyGlowNotification();

        if (SlotIcon != null && SlotIcon.enabled)
        {
            _readyNotificationTween = DOTween.Sequence()
                .Append(SlotIcon.rectTransform.DOPunchScale(
                    _baseIconScale * 0.10f,
                    0.32f,
                    2,
                    0.7f))
                .Join(SlotIcon.DOFade(_activeOpacity, 0.08f))
                .Append(SlotIcon.DOFade(_defaultOpacity, 0.22f))
                .OnComplete(StartReadyPulse)
                .SetUpdate(true);
        }
        else
        {
            StartReadyPulse();
        }
    }


    private void PlayReadyGlowNotification()
    {
        if (ReadyGlow == null)
            return;

        _glowTween?.Kill();
        ReadyGlow.enabled = true;
        ReadyGlow.color = new Color(_readyGlowColor.r, _readyGlowColor.g, _readyGlowColor.b, 0f);

        _glowTween = DOTween.Sequence()
            .Append(ReadyGlow.DOFade(0.95f, 0.10f))
            .Append(ReadyGlow.DOFade(0.42f, 0.28f))
            .SetUpdate(true)
            .OnComplete(StartReadyGlow);
    }


    private void StartReadyGlow()
    {
        if (ReadyGlow == null)
            return;

        _glowTween?.Kill();
        ReadyGlow.enabled = true;
        ReadyGlow.color = new Color(_readyGlowColor.r, _readyGlowColor.g, _readyGlowColor.b, 0f);
        _glowTween = ReadyGlow.DOFade(0.42f, 0.55f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }


    private void StopCooldownTweens()
    {
        _activationTween?.Kill();
        _readyNotificationTween?.Kill();
        _activationTween = null;
        _readyNotificationTween = null;
    }


    private void StopReadyTweens()
    {
        _readyTween?.Kill();
        _glowTween?.Kill();
        _readyTween = null;
        _glowTween = null;

        if (SlotIcon != null)
            SetIconScale(_baseIconScale);
    }


    private void StopAllVisualTweens()
    {
        _readyTween?.Kill();
        _activationTween?.Kill();
        _readyNotificationTween?.Kill();
        _glowTween?.Kill();
        _readyTween = null;
        _activationTween = null;
        _readyNotificationTween = null;
        _glowTween = null;

        if (SlotIcon != null)
            SetIconScale(_baseIconScale);
    }


    private void SetPipTint(Color tint)
    {
        foreach (Image pip in _pipImages)
        {
            if (pip != null)
                pip.color = tint;
        }
    }


    private void SetIconColor(Color color, float alpha)
    {
        if (SlotIcon == null) return;

        color.a = alpha;
        SlotIcon.color = color;
    }


    private void SetIconScale(Vector3 scale)
    {
        if (SlotIcon == null) return;

        SlotIcon.rectTransform.localScale = scale;
    }


    #endregion                  //----------------------------------------
}
