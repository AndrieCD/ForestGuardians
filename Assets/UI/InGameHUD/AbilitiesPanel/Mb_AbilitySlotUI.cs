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
//   - Activation flash: brief full-opacity pulse when the cooldown starts
//     (same "remaining jumped up" detection as Mb_ReticleUI).
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
using System.Collections;
using System.Collections.Generic;

public class Mb_AbilitySlotUI : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [SerializeField] private Image SlotIcon;
    [SerializeField] private Image CooldownOverlay;

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

    // Cooldown tracking
    private bool _isOnCooldown = false;
    private float _cooldownTotal = 0f;
    private float _cooldownRemaining = 0f;
    private float _prevCooldownValue = 0f;

    // Activation flash coroutine handle
    private Coroutine _flashCoroutine;

    // Ready scale coroutine handle
    private Coroutine _readyScaleCoroutine;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Update()
    {
        // Overlay fill — only runs while on cooldown (bool guard)
        if (!_isOnCooldown || CooldownOverlay == null || _cooldownTotal <= 0f) return;

        CooldownOverlay.fillAmount = Mathf.Clamp01(_cooldownRemaining / _cooldownTotal);
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
    bool showPips = true)   // ← add this
    {
        StopActivationFlash();
        StopReadyScale();

        _pipFilledSprite = pipFilled;
        _pipEmptySprite = pipEmpty;
        _defaultOpacity = defaultOpacity;
        _cooldownOpacity = cooldownOpacity;
        _activeOpacity = activeOpacity;
        _readyPulseScale = Mathf.Max(1f, readyPulseScale);
        _readyPulseDuration = Mathf.Max(0.01f, readyPulseDuration);

        if (SlotIcon != null)
        {
            _baseIconScale = SlotIcon.rectTransform.localScale;
            SlotIcon.sprite = icon;
            SlotIcon.enabled = icon != null;
            SetOpacity(SlotIcon, _defaultOpacity);
            SetIconScale(_baseIconScale);
        }

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

        TriggerReadyScale();
    }


    /// <summary>
    /// Shows the locked/empty state — used for R slot before branch is chosen.
    /// Hides pips and clears the overlay.
    /// </summary>
    public void ShowLocked(Sprite lockedIcon, Color overlayColor)
    {
        StopActivationFlash();
        StopReadyScale();

        if (SlotIcon != null)
        {
            _baseIconScale = SlotIcon.rectTransform.localScale;
            SlotIcon.sprite = lockedIcon;
            SlotIcon.enabled = lockedIcon != null;

            // Locked state uses a dimmed opacity so it reads as unavailable
            // TODO: Tune this value — 0.4 is visibly greyed out without being invisible.
            SetOpacity(SlotIcon, 0.4f);
            SetIconScale(_baseIconScale);
        }

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

            StopReadyScale();
            SetOpacity(SlotIcon, _cooldownOpacity);

            // Brief activation flash before settling into cooldown opacity
            StopActivationFlash();
            _flashCoroutine = StartCoroutine(ActivationFlashRoutine());
        }

        // Cooldown finished
        if (clampedRemaining <= 0f && _isOnCooldown)
        {
            _isOnCooldown = false;
            StopActivationFlash();

            if (CooldownOverlay != null)
                CooldownOverlay.fillAmount = 0f;

            SetOpacity(SlotIcon, _defaultOpacity);
            TriggerReadyScale();
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

    // Flashes to full opacity briefly when the ability activates, then
    // settles back to cooldown opacity. Gives tactile feedback on use.
    private IEnumerator ActivationFlashRoutine()
    {
        SetOpacity(SlotIcon, _activeOpacity);

        // TODO: Tune flash hold — 0.1s is a quick pulse; raise to 0.2 for more visibility.
        yield return new WaitForSeconds(0.10f);

        SetOpacity(SlotIcon, _isOnCooldown ? _cooldownOpacity : _defaultOpacity);
        _flashCoroutine = null;
    }


    private IEnumerator ReadyScaleRoutine()
    {
        while (!_isOnCooldown)
        {
            yield return ScaleIconRoutine(_baseIconScale, _baseIconScale * _readyPulseScale, _readyPulseDuration);
            yield return ScaleIconRoutine(_baseIconScale * _readyPulseScale, _baseIconScale, _readyPulseDuration);
        }

        SetIconScale(_baseIconScale);
        _readyScaleCoroutine = null;
    }


    private IEnumerator ScaleIconRoutine(Vector3 fromScale, Vector3 toScale, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetIconScale(Vector3.Lerp(fromScale, toScale, EaseInOutSine(t)));
            yield return null;
        }

        SetIconScale(toScale);
    }

    #endregion                  //----------------------------------------


    #region Helpers             //----------------------------------------

    private void StopActivationFlash()
    {
        if (_flashCoroutine == null) return;

        StopCoroutine(_flashCoroutine);
        _flashCoroutine = null;
    }


    private void TriggerReadyScale()
    {
        if (SlotIcon == null || _readyScaleCoroutine != null)
            return;

        _readyScaleCoroutine = StartCoroutine(ReadyScaleRoutine());
    }


    private void StopReadyScale()
    {
        if (_readyScaleCoroutine == null) return;

        StopCoroutine(_readyScaleCoroutine);
        _readyScaleCoroutine = null;

        if (SlotIcon != null)
            SetIconScale(_baseIconScale);
    }

    private void SetOpacity(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }


    private void SetIconScale(Vector3 scale)
    {
        if (SlotIcon == null) return;

        SlotIcon.rectTransform.localScale = scale;
    }


    private float EaseInOutSine(float t)
    {
        return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
    }

    #endregion                  //----------------------------------------
}
