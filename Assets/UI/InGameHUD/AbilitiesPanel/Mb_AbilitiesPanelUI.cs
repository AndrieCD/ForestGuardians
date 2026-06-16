// Mb_AbilitiesPanelUI.cs
// Drives the four ability slots on the HUD: Passive, Q, E, R.
//
// HOW IT WORKS:
//   - Four slots displayed in a row at the bottom center of the HUD.
//   - Each slot shows: ability icon, level pips, cooldown overlay.
//   - Opacity states match the reticle: 75% default, 50% on cooldown, 100% on activation.
//   - Cooldown overlay: child Image, fillMethod Vertical fillOrigin Bottom,
//     driven by remainingCooldown / totalCooldown, updated in Update() only
//     while _isOnCooldown is true per slot.
//   - Cooldown start detected the same way as Mb_ReticleUI: remaining jumps UP
//     above previous value — no separate OnCooldownStarted event needed.
//   - R slot starts in a locked/empty visual state until UltimateBranch is chosen.
//     On branch selection, GameManager fires OnGameStateChanged out of RewardsPanel
//     back to Playing — we use that transition to refresh the R slot.
//   - Level pips: a row of small Images, filled/unfilled based on CurrentLevel.
//     Pip count = MaxLevel (sourced from SO_Ability.MaxLevel via ability.MaxLevel).
//
// HIERARCHY (set up in the Inspector / Unity Editor):
//   AbilitiesPanel (this GameObject)
//   ├── PassiveSlot
//   │   ├── SlotIcon          (Image)
//   │   ├── CooldownOverlay   (Image — fillMethod Vertical, fillOrigin Bottom)
//   │   └── PipsContainer     (horizontal layout — holds pip Images)
//   ├── QSlot                 (same structure)
//   ├── ESlot                 (same structure)
//   └── RSlot                 (same structure — starts showing LockedIcon)
//
// Inspector Setup:
//   - GuardianObject: drag the Guardian (Player) GameObject here.
//   - PassiveSlot, QSlot, ESlot, RSlot: drag each Mb_AbilitySlotUI component.
//   - PipFilledSprite, PipEmptySprite: sprites for filled/unfilled level pips.
//   - LockedIcon: sprite shown on R slot before branch is chosen.
//   - DefaultOpacity, CooldownOpacity, ActiveOpacity: shared opacity values.
//   - OverlayColor: semi-transparent dark color for cooldown overlays.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Mb_AbilitiesPanelUI : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Header("References")]
    [Tooltip("Drag the Guardian (Player) GameObject here.")]
    [SerializeField] private GameObject GuardianObject;

    [Header("Slots")]
    [SerializeField] private Mb_AbilitySlotUI QSlot;
    [SerializeField] private Mb_AbilitySlotUI ESlot;
    [SerializeField] private Mb_AbilitySlotUI RSlot;

    [Header("Pip Sprites")]
    [Tooltip("Sprite for a filled level pip (ability has reached this level).")]
    [SerializeField] private Sprite PipFilledSprite;
    [Tooltip("Sprite for an empty level pip (level not yet reached).")]
    [SerializeField] private Sprite PipEmptySprite;

    [Header("R Slot — Locked State")]
    [Tooltip("Icon shown on the R slot before the player picks their Ultimate Branch.")]
    [SerializeField] private Sprite LockedIcon;

    [Header("Opacity States")]
    // TODO: Keep these in sync with Mb_ReticleUI values for visual consistency.
    [Range(0f, 1f)][SerializeField] private float DefaultOpacity = 0.75f;
    [Range(0f, 1f)][SerializeField] private float CooldownOpacity = 0.50f;
    [Range(0f, 1f)][SerializeField] private float ActiveOpacity = 1.00f;

    [Header("Overlay Color")]
    [SerializeField] private Color OverlayColor = new Color(0f, 0f, 0f, 0.4f);

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    private Mb_AbilityController _abilityController;

    // Cached ability references per slot
    private Sc_BaseAbility _qAbility;
    private Sc_BaseAbility _eAbility;
    private Sc_BaseAbility _rAbility;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        if (GuardianObject == null)
            Debug.LogError("[Mb_AbilitiesPanelUI] GuardianObject is not assigned.");

    }


    private void OnEnable()
    {
        FetchAndSubscribe();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }


    private void OnDisable()
    {
        UnsubscribeAll();

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }

    #endregion                  //----------------------------------------


    #region Setup               //----------------------------------------

    private void FetchAndSubscribe()
    {
        GuardianObject = GameObject.FindGameObjectWithTag("Player");

        if (GuardianObject == null) return;

        if (_abilityController == null)
            _abilityController = GuardianObject.GetComponent<Mb_AbilityController>();

        if (_abilityController == null)
        {
            Debug.LogError("[Mb_AbilitiesPanelUI] No Mb_AbilityController found on GuardianObject.");
            return;
        }

        // Fetch and bind each slot — R slot may be null until branch is chosen
        BindSlot(ref _qAbility, AbilitySlot.Q, QSlot, isRSlot: false);
        BindSlot(ref _eAbility, AbilitySlot.E, ESlot, isRSlot: false);
        BindSlot(ref _rAbility, AbilitySlot.R, RSlot, isRSlot: false);
    }


    /// <summary>
    /// Fetches the ability for a slot, initializes the slot UI, and subscribes to events.
    /// If the ability is null (R slot pre-branch), shows the locked state instead.
    /// </summary>
    // Update BindSlot signature to accept showPips:
    private void BindSlot(
        ref Sc_BaseAbility cachedRef,
        AbilitySlot slot,
        Mb_AbilitySlotUI slotUI,
        bool isRSlot,
        bool showPips = true)   // ← add this
    {
        if (slotUI == null) return;

        if (cachedRef != null)
        {
            cachedRef.OnCooldownChanged -= slotUI.HandleCooldownChanged;
            cachedRef.OnAbilityLevelChanged -= slotUI.HandleLevelChanged;
        }

        cachedRef = _abilityController.GetAbilityBySlot(slot);

        if (cachedRef == null)
        {
            slotUI.ShowLocked(LockedIcon, OverlayColor);
            return;
        }

        slotUI.Initialize(
            cachedRef.GetAbilityData().Icon,
            cachedRef.CurrentLevel,
            cachedRef.MaxLevel,
            PipFilledSprite,
            PipEmptySprite,
            DefaultOpacity,
            OverlayColor,
            showPips        // ← pass through
        );

        cachedRef.OnCooldownChanged += slotUI.HandleCooldownChanged;

        // Only subscribe to level changes if this slot can actually level up
        if (showPips)
            cachedRef.OnAbilityLevelChanged += slotUI.HandleLevelChanged;
    }


    private void UnsubscribeAll()
    {
        UnsubscribeSlot(_qAbility, QSlot);
        UnsubscribeSlot(_eAbility, ESlot);
        UnsubscribeSlot(_rAbility, RSlot);
    }


    private void UnsubscribeSlot(Sc_BaseAbility ability, Mb_AbilitySlotUI slotUI)
    {
        if (ability == null || slotUI == null) return;
        ability.OnCooldownChanged -= slotUI.HandleCooldownChanged;
        ability.OnAbilityLevelChanged -= slotUI.HandleLevelChanged;
    }

    #endregion                  //----------------------------------------


    #region Game State Handling //----------------------------------------

    private void HandleGameStateChanged(GameState newState)
    {
        // Show/hide the panel
        bool shouldShow = newState == GameState.Playing || newState == GameState.Paused || newState == GameState.RewardsPanel;
        gameObject.SetActive(shouldShow);

        // When returning to Playing from RewardsPanel, the R slot may have just
        // been filled by a branch selection — rebind it to pick up the new ability.
        // We check specifically for the Playing transition so this only fires once,
        // not on every state change.
        if (newState == GameState.Playing)
            RefreshRSlot();
    }


    // Rebinds only the R slot — called after a branch may have been selected.
    // Safe to call multiple times; the slot shows locked if R is still null.
    private void RefreshRSlot()
    {
        if (_abilityController == null) return;

        Sc_BaseAbility currentR = _abilityController.GetAbilityBySlot(AbilitySlot.R);

        // Only rebind if the slot changed — avoids redundant work every state transition
        if (currentR == _rAbility) return;

        BindSlot(ref _rAbility, AbilitySlot.R, RSlot, isRSlot: true);
    }

    #endregion                  //----------------------------------------
}