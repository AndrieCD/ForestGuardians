// Mb_ReticleUI.cs
// Drives the reticle and Primary (LMB) / Secondary (RMB) attack icons on the HUD.
//
// HOW IT WORKS:
//   - Reticle Image sits at screen center (anchor to center in the Inspector).
//   - LMB icon sits to the left of the reticle, RMB icon to the right.
//   - Each icon has three visual states:
//       Available  : 75% opacity, default position
//       OnCooldown : 50% opacity, default position, cooldown overlay fills up
//       Activated  : 100% opacity, moves closer to reticle, scales up slightly
//   - LMB held-down behavior:
//       LMB icon  → scale up, move toward reticle, 100% opacity
//       RMB icon  → move slightly toward reticle, drop to 50% opacity (suppressed)
//   - Cooldown overlay: child Image per icon, fillMethod Vertical fillOrigin Bottom,
//     darkened semi-transparent, driven by remainingCooldown / totalCooldown.
//     Updated in Update() only while _isOnCooldown flag is true.
//   - Total cooldown duration is cached when OnCooldownChanged fires a value
//     GREATER than the previous remaining — that transition = cooldown just started.
//
// HIERARCHY (set up in the Inspector / Unity Editor):
//   Reticle Root (this GameObject)
//   ├── ReticleImage          (Image — centered)
//   ├── LMB_Icon              (Image — left of reticle)
//   │   └── LMB_CooldownOverlay  (Image — same size, fillMethod Vertical, ~40% black)
//   └── RMB_Icon              (Image — right of reticle)
//       └── RMB_CooldownOverlay  (Image — same size, fillMethod Vertical, ~40% black)
//
// Inspector Setup:
//   - GuardianObject: drag the Guardian (Player) GameObject here.
//   - ReticleImage: the center crosshair Image.
//   - LMBIcon, RMBIcon: the attack icon Images.
//   - LMBOverlay, RMBOverlay: the cooldown overlay Images (child of each icon).
//   - LMBIconRect, RMBIconRect: RectTransforms of the icon Images (for position/scale anim).
//   - DefaultOpacity, CooldownOpacity, ActiveOpacity: tunable opacity states.
//   - ActivatedScaleMultiplier: how much the LMB icon scales up when activated.
//   - ActivatedMoveDistance: pixels the icon moves toward the reticle on activation.
//   - AnimLerpSpeed: how fast position/scale lerps animate.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_ReticleUI : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Header("References")]
    [Tooltip("Drag the Guardian (Player) GameObject here.")]
    [SerializeField] private GameObject GuardianObject;

    [Header("UI Elements")]
    [SerializeField] private Image ReticleImage;
    [SerializeField] private Image LMBIcon;
    [SerializeField] private Image RMBIcon;

    [Header("Cooldown Overlays")]
    [Tooltip("Child Image on LMBIcon. fillMethod = Vertical, fillOrigin = Bottom.")]
    [SerializeField] private Image LMBOverlay;
    [Tooltip("Child Image on RMBIcon. fillMethod = Vertical, fillOrigin = Bottom.")]
    [SerializeField] private Image RMBOverlay;

    [Header("RectTransforms (for animation)")]
    [Tooltip("RectTransform of the LMB icon — used for position and scale animation.")]
    [SerializeField] private RectTransform LMBIconRect;
    [Tooltip("RectTransform of the RMB icon — used for position and scale animation.")]
    [SerializeField] private RectTransform RMBIconRect;

    [Header("Opacity States")]
    // TODO: Tweak these values to match your art style.
    [Range(0f, 1f)][SerializeField] private float DefaultOpacity = 0.5f;
    [Range(0f, 1f)][SerializeField] private float CooldownOpacity = 0.25f;
    [Range(0f, 1f)][SerializeField] private float ActiveOpacity = 0.75f;

    [Header("Activation Animation")]
    // TODO: Tune ActivatedScaleMultiplier — 1.2 is a subtle pop; raise to 1.35 for more punch.
    [Tooltip("Scale multiplier applied to the LMB icon when activated.")]
    [SerializeField] private float ActivatedScaleMultiplier = 1.2f;

    // TODO: Tune ActivatedMoveDistance — 12px feels close without being distracting.
    [Tooltip("Pixels the icon moves toward the reticle on activation.")]
    [SerializeField] private float ActivatedMoveDistance = 12f;

    // TODO: Tune AnimLerpSpeed — 18 is snappy; lower to ~10 for a floatier feel.
    [Tooltip("Speed of position and scale lerp animations.")]
    [SerializeField] private float AnimLerpSpeed = 18f;

    [Header("Overlay Color")]
    // TODO: Adjust alpha to taste — 0.4 is readable without obscuring the icon art.
    [Tooltip("Color of the cooldown overlay. Keep dark and semi-transparent.")]
    [SerializeField] private Color OverlayColor = new Color(0f, 0f, 0f, 0.4f);

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    // Cached ability references — fetched once after Guardian is set
    private Sc_BaseAbility _primaryAbility;
    private Sc_BaseAbility _secondaryAbility;
    private Mb_AbilityController _abilityController;

    // --- LMB state ---
    private bool _lmbOnCooldown = false;
    private bool _lmbActivated = false;
    private float _lmbCooldownTotal = 0f;
    private float _lmbCooldownRemaining = 0f;
    // Previous cooldown value — used to detect when a new cooldown starts
    // (remaining jumps UP from a low value, meaning a fresh cooldown fired)
    private float _lmbPrevCooldown = 0f;

    // --- RMB state ---
    private bool _rmbOnCooldown = false;
    private bool _rmbActivated = false;
    private float _rmbCooldownTotal = 0f;
    private float _rmbCooldownRemaining = 0f;
    private float _rmbPrevCooldown = 0f;

    // --- Animation targets ---
    // Default anchor positions are cached in Awake so we can always return to them
    private Vector2 _lmbDefaultAnchoredPos;
    private Vector2 _rmbDefaultAnchoredPos;
    private Vector3 _defaultIconScale = Vector3.one;

    // Current animated position and scale targets — lerped toward in Update()
    private Vector2 _lmbTargetPos;
    private Vector2 _rmbTargetPos;
    private Vector3 _lmbTargetScale;
    private Vector3 _rmbTargetScale;

    // Coroutine handle for deactivation — lets us cancel a running deactivate
    // if the player fires again before the previous animation finishes
    private Coroutine _lmbDeactivateCoroutine;
    private Coroutine _rmbDeactivateCoroutine;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
       
    }


    private void Start()
    {
        // Cache default positions and scale so animations always know where to return
        if (LMBIconRect != null)
        {
            _lmbDefaultAnchoredPos = LMBIconRect.anchoredPosition;
            _defaultIconScale = LMBIconRect.localScale;
        }
        if (RMBIconRect != null)
            _rmbDefaultAnchoredPos = RMBIconRect.anchoredPosition;

        // Initialize animation targets to defaults
        _lmbTargetPos = _lmbDefaultAnchoredPos;
        _rmbTargetPos = _rmbDefaultAnchoredPos;
        _lmbTargetScale = _defaultIconScale;
        _rmbTargetScale = _defaultIconScale;

        // Apply overlay color
        if (LMBOverlay != null) LMBOverlay.color = OverlayColor;
        if (RMBOverlay != null) RMBOverlay.color = OverlayColor;

        // Start overlays empty
        if (LMBOverlay != null) LMBOverlay.fillAmount = 0f;
        if (RMBOverlay != null) RMBOverlay.fillAmount = 0f;

        // Set default opacities
        SetIconOpacity(LMBIcon, DefaultOpacity);
        SetIconOpacity(RMBIcon, DefaultOpacity);


        // Start() is now only used to log a final confirmation that refs are ready.
        // All fetching and subscribing happens in OnEnable so the order is guaranteed.
        if (_primaryAbility == null)
            Debug.LogError("[Mb_ReticleUI] _primaryAbility is still null after Start(). " +
                           "Check that GuardianObject is assigned and abilities are set in Awake.");
        if (_secondaryAbility == null)
            Debug.LogError("[Mb_ReticleUI] _secondaryAbility is still null after Start(). " +
                           "Check that GuardianObject is assigned and abilities are set in Awake.");
    }


    private void OnEnable()
    {
        // Fetch ability refs here — OnEnable can fire before Start() on the first frame,
        // so refs must be ready BEFORE we try to subscribe. The null guard means it's
        // safe to call this multiple times.
        FetchAbilityRefs();
        SubscribeToAbilities();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }


    private void OnDisable()
    {
        UnsubscribeFromAbilities();

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }


    private void Update()
    {
        // --- Cooldown overlay fills ---
        // Only updated while on cooldown — bool guards prevent unconditional polling.
        if (_lmbOnCooldown && _lmbCooldownTotal > 0f)
        {
            float fill = _lmbCooldownRemaining / _lmbCooldownTotal;
            if (LMBOverlay != null)
                LMBOverlay.fillAmount = Mathf.Clamp01(fill);
        }

        if (_rmbOnCooldown && _rmbCooldownTotal > 0f)
        {
            float fill = _rmbCooldownRemaining / _rmbCooldownTotal;
            if (RMBOverlay != null)
                RMBOverlay.fillAmount = Mathf.Clamp01(fill);
        }

        // --- Position and scale animation ---
        // Lerp icon RectTransforms toward their current targets each frame.
        // Targets are set by activation/deactivation logic below.
        if (LMBIconRect != null)
        {
            LMBIconRect.anchoredPosition = Vector2.Lerp(
                LMBIconRect.anchoredPosition, _lmbTargetPos, AnimLerpSpeed * Time.deltaTime);
            LMBIconRect.localScale = Vector3.Lerp(
                LMBIconRect.localScale, _lmbTargetScale, AnimLerpSpeed * Time.deltaTime);
        }

        if (RMBIconRect != null)
        {
            RMBIconRect.anchoredPosition = Vector2.Lerp(
                RMBIconRect.anchoredPosition, _rmbTargetPos, AnimLerpSpeed * Time.deltaTime);
            RMBIconRect.localScale = Vector3.Lerp(
                RMBIconRect.localScale, _rmbTargetScale, AnimLerpSpeed * Time.deltaTime);
        }
    }

    #endregion                  //----------------------------------------


    #region Cooldown Handlers   //----------------------------------------

    private void HandlePrimaryCooldownChanged(float remaining)
    {
        Debug.Log("[Mb_ReticleUI] Primary cooldown changed: " + remaining);
        _lmbCooldownRemaining = remaining;

        // Detect cooldown start: remaining jumped UP compared to last frame.
        // This happens exactly once per activation — when the ability fires and
        // StartCooldown() sets _CooldownRemaining to the full duration.
        if (remaining > _lmbPrevCooldown + 0.05f)
        {
            // New cooldown started — cache the total duration
            _lmbCooldownTotal = remaining;
            _lmbOnCooldown = true;

            SetIconOpacity(LMBIcon, CooldownOpacity);

            // Trigger the activated animation then return to cooldown state
            ActivateLMB();
        }

        // Cooldown finished
        if (remaining <= 0f && _lmbOnCooldown)
        {
            _lmbOnCooldown = false;

            if (LMBOverlay != null) LMBOverlay.fillAmount = 0f;

            // Only restore to default if not currently showing the activated state
            if (!_lmbActivated)
                SetIconOpacity(LMBIcon, DefaultOpacity);
        }

        _lmbPrevCooldown = remaining;
    }


    private void HandleSecondaryCooldownChanged(float remaining)
    {
        Debug.Log("[Mb_ReticleUI] Secondary cooldown changed: " + remaining);

        _rmbCooldownRemaining = remaining;

        if (remaining > _rmbPrevCooldown + 0.05f)
        {
            _rmbCooldownTotal = remaining;
            _rmbOnCooldown = true;

            SetIconOpacity(RMBIcon, CooldownOpacity);

            ActivateRMB();
        }

        if (remaining <= 0f && _rmbOnCooldown)
        {
            _rmbOnCooldown = false;

            if (RMBOverlay != null) RMBOverlay.fillAmount = 0f;

            if (!_rmbActivated)
                SetIconOpacity(RMBIcon, DefaultOpacity);
        }

        _rmbPrevCooldown = remaining;
    }

    #endregion                  //----------------------------------------


    #region Activation Animation //----------------------------------------

    // LMB activated:
    //   - LMB icon  → scale up, move toward reticle, full opacity
    //   - RMB icon  → slight move toward reticle, suppressed opacity
    private void ActivateLMB()
    {
        _lmbActivated = true;

        // LMB moves RIGHT (toward center) — its default pos is to the LEFT of reticle
        // so we subtract from X to push it rightward (toward center)
        _lmbTargetPos = _lmbDefaultAnchoredPos + new Vector2(ActivatedMoveDistance, 0f);
        _lmbTargetScale = _defaultIconScale * ActivatedScaleMultiplier;
        SetIconOpacity(LMBIcon, ActiveOpacity);

        // Suppress RMB — slight inward nudge + lower opacity
        // RMB is to the RIGHT so we subtract X to push it leftward (toward center)
        //_rmbTargetPos = _rmbDefaultAnchoredPos + new Vector2(-ActivatedMoveDistance * 0.4f, 0f);
        SetIconOpacity(RMBIcon, CooldownOpacity);

        // Cancel any running deactivate coroutine and start a fresh one
        if (_lmbDeactivateCoroutine != null)
            StopCoroutine(_lmbDeactivateCoroutine);
        _lmbDeactivateCoroutine = StartCoroutine(DeactivateLMBRoutine());
    }


    private void ActivateRMB()
    {
        _rmbActivated = true;

        _rmbTargetPos = _rmbDefaultAnchoredPos + new Vector2(-ActivatedMoveDistance, 0f);
        _rmbTargetScale = _defaultIconScale * ActivatedScaleMultiplier;
        SetIconOpacity(RMBIcon, ActiveOpacity);

        //_lmbTargetPos = _lmbDefaultAnchoredPos + new Vector2(ActivatedMoveDistance * 0.4f, 0f);
        SetIconOpacity(LMBIcon, CooldownOpacity);

        if (_rmbDeactivateCoroutine != null)
            StopCoroutine(_rmbDeactivateCoroutine);
        _rmbDeactivateCoroutine = StartCoroutine(DeactivateRMBRoutine());
    }


    // Holds the activated state briefly then returns the icon to its resting state.
    // The actual lerp back happens in Update() once the targets are reset here.
    private IEnumerator DeactivateLMBRoutine()
    {
        // TODO: Tune hold duration — 0.12s is barely perceptible but gives the
        //       icon a moment to reach its activated position before snapping back.
        yield return new WaitForSeconds(0.12f);

        _lmbActivated = false;
        _lmbTargetPos = _lmbDefaultAnchoredPos;
        _lmbTargetScale = _defaultIconScale;

        // Restore opacity: on cooldown = CooldownOpacity, else DefaultOpacity
        SetIconOpacity(LMBIcon, _lmbOnCooldown ? CooldownOpacity : DefaultOpacity);

        // Restore RMB suppression
        //_rmbTargetPos = _rmbDefaultAnchoredPos;
        SetIconOpacity(RMBIcon, _rmbOnCooldown ? CooldownOpacity : DefaultOpacity);

        _lmbDeactivateCoroutine = null;
    }


    private IEnumerator DeactivateRMBRoutine()
    {
        yield return new WaitForSeconds(0.12f);

        _rmbActivated = false;
        _rmbTargetPos = _rmbDefaultAnchoredPos;
        _rmbTargetScale = _defaultIconScale;

        SetIconOpacity(RMBIcon, _rmbOnCooldown ? CooldownOpacity : DefaultOpacity);

        //_lmbTargetPos = _lmbDefaultAnchoredPos;
        SetIconOpacity(LMBIcon, _lmbOnCooldown ? CooldownOpacity : DefaultOpacity);

        _rmbDeactivateCoroutine = null;
    }

    #endregion                  //----------------------------------------


    #region Show / Hide         //----------------------------------------

    private void HandleGameStateChanged(GameState newState)
    {
        bool shouldShow = newState == GameState.Playing || newState == GameState.Paused || newState == GameState.RewardsPanel;
        gameObject.SetActive(shouldShow);
    }

    #endregion                  //----------------------------------------


    #region Subscriptions       //----------------------------------------

    // Separated from OnEnable so they can be called again if abilities are
    // reassigned mid-game (e.g. after branch selection replaces R slot).
    // Primary and Secondary don't change, but the pattern is consistent.
    private void SubscribeToAbilities()
    {
        if (_primaryAbility != null)
        {
            _primaryAbility.OnCooldownChanged -= HandlePrimaryCooldownChanged;
            _primaryAbility.OnCooldownChanged += HandlePrimaryCooldownChanged;
        }

        if (_secondaryAbility != null)
        {
            _secondaryAbility.OnCooldownChanged -= HandleSecondaryCooldownChanged;
            _secondaryAbility.OnCooldownChanged += HandleSecondaryCooldownChanged;
        }
    }


    private void UnsubscribeFromAbilities()
    {
        if (_primaryAbility != null)
            _primaryAbility.OnCooldownChanged -= HandlePrimaryCooldownChanged;

        if (_secondaryAbility != null)
            _secondaryAbility.OnCooldownChanged -= HandleSecondaryCooldownChanged;
    }

    private void FetchAbilityRefs()
    {
        if (GuardianObject == null)
        {
            Debug.LogError("[Mb_ReticleUI] GuardianObject is not assigned in the Inspector.");
            return;
        }

        // Only fetch if not already cached — avoids redundant GetComponent calls
        if (_abilityController == null)
            _abilityController = GuardianObject.GetComponent<Mb_AbilityController>();

        if (_abilityController == null)
        {
            Debug.LogError("[Mb_ReticleUI] No Mb_AbilityController found on GuardianObject.");
            return;
        }

        // Fetch if null — Primary/Secondary don't change at runtime so one fetch is enough
        if (_primaryAbility == null)
        {
            _primaryAbility = _abilityController.GetAbilityBySlot(AbilitySlot.Primary);
            Debug.Log($"[Mb_ReticleUI] Primary ability fetched: " +
                      $"{(_primaryAbility != null ? _primaryAbility.GetType().Name : "NULL")}");
        }

        if (_secondaryAbility == null)
        {
            _secondaryAbility = _abilityController.GetAbilityBySlot(AbilitySlot.Secondary);
            Debug.Log($"[Mb_ReticleUI] Secondary ability fetched: " +
                      $"{(_secondaryAbility != null ? _secondaryAbility.GetType().Name : "NULL")}");
        }
    }

    #endregion                  //----------------------------------------


    #region Helpers             //----------------------------------------

    private void SetIconOpacity(Image icon, float alpha)
    {
        if (icon == null) return;
        Color c = icon.color;
        c.a = alpha;
        icon.color = c;
    }

    #endregion                  //----------------------------------------
}