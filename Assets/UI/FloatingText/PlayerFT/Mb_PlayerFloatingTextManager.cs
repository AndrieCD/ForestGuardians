// Mb_PlayerFloatingTextManager.cs
// Drives all floating combat text on the player's HUD.
//
// HOW IT WORKS:
//   - Lives on the HUD Canvas in the Stage scene.
//   - Subscribes to the player's Mb_HealthComponent events to detect damage,
//     healing, and shield gains. Tracks previous health to calculate deltas.
//   - Subscribes to the player's Mb_StatusEffectController.OnStatusApplied
//     to display status labels when effects are applied.
//   - Spawns the correct floating text type (color, label) for each event.
//   - Maintains a pool of Mb_PlayerFloatingText instances parented to this Canvas.
//
// Inspector setup:
//   - AnchorZone: a RectTransform on the HUD that marks where texts spawn.
//     Position it freely in the UI — the code reads its screen position at runtime.
//   - PlayerObject: drag the Guardian GameObject here.
//   - InitialPoolSize: how many text elements to pre-warm (default 10).
//   - FloatSpeed: upward pixels-per-second for the slide animation.
//   - FadeDuration: total seconds from spawn to fully transparent.
//   - HorizontalSpread: max random pixel offset left/right to prevent overlap.
//   - DamageColor, HealColor, ShieldColor: colors per event type.
//   - StatusColors: per-status color overrides. Add entries in the Inspector.

using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Mb_PlayerFloatingTextManager : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Header("Anchor")]
    [Tooltip("RectTransform on the HUD that marks where floating texts spawn. " +
             "Move this freely in the UI — no code changes needed.")]
    [SerializeField] private RectTransform AnchorZone;

    [Header("References")]
    [Tooltip("Drag the Guardian (Player) GameObject here.")]
    [SerializeField] private GameObject PlayerObject;

    [Tooltip("Prefab containing RectTransform, TMP_Text, and Mb_PlayerFloatingText.")]
    [SerializeField] private Mb_PlayerFloatingText FloatingTextPrefab;

    [Header("Pool")]
    // TODO: Tune InitialPoolSize — 10 covers most burst scenarios (Q + E + damage in one frame).
    [SerializeField] private int InitialPoolSize = 10;

    [Header("Animation")]
    [Tooltip("Upward slide speed in pixels per second.")]
    [SerializeField] private float FloatSpeed = 80f;

    [Tooltip("Total time in seconds from spawn to fully faded.")]
    [SerializeField] private float FadeDuration = 1.2f;

    [Tooltip("Max random horizontal offset in pixels to prevent texts stacking perfectly.")]
    [SerializeField] private float HorizontalSpread = 30f;

    [Header("Colors")]
    [SerializeField] private Color DamageColor = Color.red;
    [SerializeField] private Color HealColor = Color.green;
    [SerializeField] private Color ShieldColor = new Color(0.4f, 0.7f, 1f); // light blue

    [Header("Status Effect Colors")]
    [Tooltip("Add one entry per StatusType to give each effect its own color. " +
             "If a StatusType has no entry here, white is used as fallback.")]
    [SerializeField] private List<StatusColorEntry> StatusColors = new List<StatusColorEntry>();

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    // Pool of inactive floating text elements ready for reuse
    private Queue<Mb_PlayerFloatingText> _pool = new Queue<Mb_PlayerFloatingText>();

    // Cached references to the player's components
    private Mb_HealthComponent _playerHealth;
    private Mb_StatusEffectController _statusEffectController;

    // Tracks the player's health value from the previous OnHealthChanged call.
    // We need this because OnHealthChanged gives us (current, max) — not the delta.
    // Delta = previousHealth - currentHealth → positive = damage, negative = heal.
    private float _previousHealth = -1f;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        if (FloatingTextPrefab == null)
            Debug.LogError("[Mb_PlayerFloatingTextManager] FloatingTextPrefab is not assigned.");

        if (AnchorZone == null)
            Debug.LogError("[Mb_PlayerFloatingTextManager] AnchorZone is not assigned.");

        PrewarmPool();
    }


    private void Start()
    {
        // Start() rather than Awake() — PlayerObject may not be fully initialized
        // during Awake on the first frame, so we fetch components here.
        if (PlayerObject != null)
        {
            _playerHealth = PlayerObject.GetComponent<Mb_HealthComponent>();
            _statusEffectController = PlayerObject.GetComponent<Mb_StatusEffectController>();
        }

        if (_playerHealth == null)
            Debug.LogError("[Mb_PlayerFloatingTextManager] Could not find Mb_HealthComponent " +
                           "on PlayerObject. Assign the correct GameObject in the Inspector.");

        // Warn but don't error — status text is optional if the controller isn't
        // attached to the Guardian yet (e.g. early prototype without status effects)
        if (_statusEffectController == null)
            Debug.LogWarning("[Mb_PlayerFloatingTextManager] No Mb_StatusEffectController found " +
                             "on PlayerObject. Status effect text will not display.");
    }


    private void OnEnable()
    {
        // We subscribe via a helper because OnEnable can fire before Start(),
        // meaning _playerHealth and _statusEffectController may not be cached yet.
        // The helper guards against null before subscribing.
        SubscribeToComponents();
    }


    private void OnDisable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
            _playerHealth.OnShieldChanged -= HandleShieldChanged;
        }

        if (_statusEffectController != null)
            _statusEffectController.OnStatusApplied -= HandleStatusApplied;
    }

    #endregion                  //----------------------------------------


    #region Event Handlers      //----------------------------------------

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        // On the very first call after the Guardian initializes, we just record
        // the starting health and don't show any text — there's no meaningful delta yet.
        if (_previousHealth < 0f)
        {
            _previousHealth = currentHealth;
            return;
        }

        float delta = _previousHealth - currentHealth;
        _previousHealth = currentHealth;

        // A positive delta means health went DOWN — damage taken
        if (delta > 0.01f)
        {
            SpawnText(
                text: $"-{Mathf.CeilToInt(delta)}",
                color: DamageColor,
                fontSizeMultiplier: 1.0f
            );
        }
        // A negative delta means health went UP — healing received
        else if (delta < -0.01f)
        {
            SpawnText(
                text: $"+{Mathf.CeilToInt(-delta)}",
                color: HealColor,
                fontSizeMultiplier: 1.0f
            );
        }
        // Deltas smaller than 0.01 are floating-point noise — ignore them
    }


    private void HandleShieldChanged(float currentShield)
    {
        // OnShieldChanged fires whenever shield is added OR consumed.
        // We only show text when a shield is GAINED (value meaningfully positive),
        // not when it's being absorbed by incoming damage.
        if (currentShield > 0.01f)
        {
            SpawnText(
                text: $"+{Mathf.CeilToInt(currentShield)} SHIELD",
                color: ShieldColor,
                fontSizeMultiplier: 0.85f
            );
        }
    }


    private void HandleStatusApplied(StatusType statusType)
    {
        // Display the status name as an uppercase label with its configured color.
        // ToString() on an enum gives the member name — "MoveSlow", "Burn", etc.
        // We format it with spaces for readability: "MoveSlow" → "MOVE SLOW"
        string label = FormatStatusLabel(statusType);
        Color color = GetStatusColor(statusType);

        SpawnText(
            text: label,
            color: color,
            fontSizeMultiplier: 0.75f  // Status labels are smaller than damage numbers
        );
    }

    #endregion                  //----------------------------------------


    #region Spawn               //----------------------------------------

    // Core spawn method — pulls an element from the pool and activates it.
    private void SpawnText(string text, Color color, float fontSizeMultiplier)
    {
        if (AnchorZone == null) return;

        Mb_PlayerFloatingText element = GetOrCreateElement();

        // Random horizontal offset so rapid events don't stack perfectly
        float horizontalOffset = Random.Range(-HorizontalSpread, HorizontalSpread);

        // Read the anchor's current screen position fresh each spawn
        // so layout changes (e.g. resolution) are automatically respected.
        // Passing null as the camera works correctly for Screen Space — Overlay canvases.
        Vector2 anchorScreenPos = RectTransformUtility.WorldToScreenPoint(
            null,
            AnchorZone.position
        );

        element.Activate(
            anchorScreenPos,
            text,
            color,
            FloatSpeed,
            FadeDuration,
            horizontalOffset,
            fontSizeMultiplier
        );
    }

    #endregion                  //----------------------------------------


    #region Pool Management     //----------------------------------------

    private void PrewarmPool()
    {
        for (int i = 0; i < InitialPoolSize; i++)
        {
            Mb_PlayerFloatingText element = CreateElement();
            element.gameObject.SetActive(false);
            _pool.Enqueue(element);
        }

        Debug.Log($"[Mb_PlayerFloatingTextManager] Pool pre-warmed with {InitialPoolSize} elements.");
    }


    private Mb_PlayerFloatingText GetOrCreateElement()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        Debug.LogWarning("[Mb_PlayerFloatingTextManager] Pool exhausted — creating new element. " +
                         "Consider raising InitialPoolSize.");

        return CreateElement();
    }


    private Mb_PlayerFloatingText CreateElement()
    {
        Mb_PlayerFloatingText element = Instantiate(
            FloatingTextPrefab,
            parent: transform
        );

        // Subscribe to the element's completion event so we know when to pool it
        element.OnReadyForPool += HandleElementReadyForPool;

        return element;
    }


    private void HandleElementReadyForPool(Mb_PlayerFloatingText element)
    {
        _pool.Enqueue(element);
    }

    #endregion                  //----------------------------------------


    #region Status Helpers      //----------------------------------------

    // Looks up the configured color for a given StatusType.
    // Falls back to white if no matching entry was added in the Inspector.
    private Color GetStatusColor(StatusType statusType)
    {
        foreach (StatusColorEntry entry in StatusColors)
        {
            if (entry.StatusType == statusType)
                return entry.Color;
        }

        return Color.white;
    }


    // Converts a StatusType enum value to a spaced, uppercase display string.
    // Examples: MoveSlow → "MOVE SLOW", AttackSlow → "ATTACK SLOW", Burn → "BURN"
    // This uses a simple approach — split on uppercase letters and join with a space.
    private string FormatStatusLabel(StatusType statusType)
    {
        string raw = statusType.ToString();

        // Insert a space before each uppercase letter that follows a lowercase letter.
        // "MoveSlow" → "Move Slow" → "MOVE SLOW"
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < raw.Length; i++)
        {
            // If this character is uppercase and the previous was lowercase, insert a space
            if (i > 0 && char.IsUpper(raw[i]) && char.IsLower(raw[i - 1]))
                sb.Append(' ');

            sb.Append(raw[i]);
        }

        return sb.ToString().ToUpper();
    }

    #endregion                  //----------------------------------------


    #region Component Subscriptions //--------------------------------------

    // Subscribes to both health and status effect events.
    // Called from both OnEnable() and Start() — the null guards ensure
    // it's safe to call from whichever fires first.
    private void SubscribeToComponents()
    {
        if (_playerHealth == null && PlayerObject != null)
            _playerHealth = PlayerObject.GetComponent<Mb_HealthComponent>();

        if (_statusEffectController == null && PlayerObject != null)
            _statusEffectController = PlayerObject.GetComponent<Mb_StatusEffectController>();

        if (_playerHealth != null)
        {
            // Unsubscribe before subscribing to prevent duplicate listeners
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
            _playerHealth.OnHealthChanged += HandleHealthChanged;

            _playerHealth.OnShieldChanged -= HandleShieldChanged;
            _playerHealth.OnShieldChanged += HandleShieldChanged;
        }

        if (_statusEffectController != null)
        {
            _statusEffectController.OnStatusApplied -= HandleStatusApplied;
            _statusEffectController.OnStatusApplied += HandleStatusApplied;
        }
    }


    // Resets the tracked previous health — call this if the Guardian
    // is re-initialized mid-stage so the delta calculation doesn't
    // produce a false large damage number on the next health event.
    public void ResetHealthTracking()
    {
        _previousHealth = -1f;
    }

    #endregion                  //----------------------------------------
}


// -------------------------------------------------------------------------
// Supporting Types
// -------------------------------------------------------------------------

/// <summary>
/// Pairs a StatusType with the color its floating text should display in.
/// Add entries in the Inspector on Mb_PlayerFloatingTextManager.
/// </summary>
[Serializable]
public struct StatusColorEntry
{
    [Tooltip("The status effect type this color applies to.")]
    public StatusType StatusType;

    [Tooltip("The color this status effect's floating text will display in.")]
    public Color Color;
}