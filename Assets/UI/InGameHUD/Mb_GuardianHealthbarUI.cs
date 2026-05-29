// Mb_GuardianHealthbarUI.cs
// Replaces the Mb_Healthbar.cs prototype.
// Drives the Guardian's health and shield bar on the HUD Canvas.
//
// HOW IT WORKS:
//   - Set the GuardianObject reference in the Inspector — no FindFirstObjectByType.
//   - Subscribes to Mb_HealthComponent events via OnEnable / OnDisable (pool-safe pattern).
//   - Three layered Image fills on the same bar:
//       HPFill        — the live health bar; color shifts green/yellow/red
//       GhostFill     — briefly shows pre-damage HP, then lerps down to match HPFill
//       ShieldFill    — overlay on top; fills proportional to currentShield / maxHealth
//   - Show/hide is tied to GameManager.OnGameStateChanged.
//
// HIERARCHY (set up in the Inspector / Unity Editor):
//   HealthbarRoot (this GameObject)
//   ├── GhostFill  (Image — fillMethod Horizontal, same width as bar)
//   ├── HPFill     (Image — fillMethod Horizontal, stacked on top of Ghost)
//   ├── ShieldFill (Image — fillMethod Horizontal, stacked on top of HP)
//   └── HPText     (TMP_Text — displays current HP as a whole number)
//
// Inspector Setup:
//   - GuardianObject: drag the Guardian (Player) GameObject here.
//   - HPFill, GhostFill, ShieldFill: drag the corresponding Image components.
//   - HPText: drag the TMP_Text label.
//   - GhostLerpSpeed: how fast the ghost bar catches up (default 3.0).
//   - GhostDelaySeconds: how long before the ghost starts chasing HP (default 0.6).

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_GuardianHealthbarUI : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Header("References")]
    [Tooltip("Drag the Guardian (Player) GameObject here.")]
    [SerializeField] private GameObject GuardianObject;

    [Header("Bar Images")]
    [Tooltip("The live HP fill image. fillMethod must be Horizontal.")]
    [SerializeField] private Image HPFill;

    [Tooltip("Ghost fill — briefly shows pre-damage HP before catching up. fillMethod Horizontal.")]
    [SerializeField] private Image GhostFill;

    [Tooltip("Shield overlay fill — stacked on top of HP. fillMethod Horizontal.")]
    [SerializeField] private Image ShieldFill;

    [Header("Text")]
    [SerializeField] private TMP_Text HPText;
    [SerializeField] private TMP_Text MaxHPText;

    [Header("Ghost Bar Tuning")]
    // TODO: Tune GhostLerpSpeed — 3.0 feels snappy; raise to 5+ for faster catch-up.
    [Tooltip("How fast the ghost bar lerps down to match HP fill.")]
    [SerializeField] private float GhostLerpSpeed = 3.0f;

    // TODO: Tune GhostDelaySeconds — 0.6s gives a brief "flash" before the ghost moves.
    [Tooltip("Seconds after taking damage before the ghost bar starts moving.")]
    [SerializeField] private float GhostDelaySeconds = 0.6f;

    [Header("Color Thresholds")]
    // TODO: Adjust thresholds if the design spec changes — current values match original prototype.
    [SerializeField] private Color ColorHealthy = Color.green;   // above 60%
    [SerializeField] private Color ColorCaution = Color.yellow;  // 30–60%
    [SerializeField] private Color ColorDanger = Color.red;     // below 30%

    [Header("Shield Fill Color")]
    // TODO: Match this to the art style — light blue is a readable default.
    [SerializeField] private Color ShieldColor = new Color(0.4f, 0.7f, 1f, 0.85f);

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    private Mb_HealthComponent _healthComponent;
    private Mb_StatBlock _statBlock;

    // Cached max HP — refreshed each time OnHealthChanged fires so level-up
    // stat increases are automatically reflected.
    private float _cachedMaxHP = 1f;

    // Ghost bar target — set to the current HPFill amount before a damage hit
    private float _ghostFillTarget = 1f;

    // Coroutine handle for the ghost delay — stored so we can restart it on rapid hits
    private Coroutine _ghostDelayCoroutine;

    // Whether the ghost is currently in its delay phase (not yet chasing HPFill)
    private bool _ghostWaiting = false;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {

    }

    private void Start()
    {
        if (GuardianObject != null)
        {
            _healthComponent = GuardianObject.GetComponent<Mb_HealthComponent>();
            _statBlock = GuardianObject.GetComponent<Mb_StatBlock>();
        }

        if (_healthComponent == null)
            Debug.LogError("[Mb_GuardianHealthbarUI] Could not find Mb_HealthComponent on GuardianObject.");

        if (_statBlock == null)
            Debug.LogError("[Mb_GuardianHealthbarUI] Could not find Mb_StatBlock on GuardianObject.");

        if (HPFill == null)
            Debug.LogError("[Mb_GuardianHealthbarUI] HPFill Image is not assigned.");

        // Initialize shield fill color once — it doesn't change at runtime
        if (ShieldFill != null)
            ShieldFill.color = ShieldColor;
    }


    private void OnEnable()
    {
        // Subscribe via OnEnable so the bar is always wired when visible
        if (_healthComponent != null)
        {
            _healthComponent.OnHealthChanged += HandleHealthChanged;
            _healthComponent.OnShieldChanged += HandleShieldChanged;
            _statBlock.MaxHealth.OnStatChanged += UpdateMaxHealth;
        }

        //// Listen for game state changes to show/hide this HUD element
        //if (GameManager.Instance != null)
        //    GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

        // Force an immediate refresh so the bar matches current health when re-enabled
        RefreshFromCurrentHealth();
    }

    private void OnDisable()
    {
        if (_healthComponent != null)
        {
            _healthComponent.OnHealthChanged -= HandleHealthChanged;
            _healthComponent.OnShieldChanged -= HandleShieldChanged;
        }

        //if (GameManager.Instance != null)
        //    GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }


    private void Update()
    {
        // Ghost bar chases HPFill — only runs while there's a meaningful gap to close.
        // This is the one intentional Update() poll in this script; everything else
        // is event-driven. The bool guard prevents running every frame at zero cost.
        if (GhostFill == null || _ghostWaiting) return;

        if (Mathf.Abs(GhostFill.fillAmount - HPFill.fillAmount) > 0.001f)
        {
            GhostFill.fillAmount = Mathf.Lerp(
                GhostFill.fillAmount,
                HPFill.fillAmount,
                GhostLerpSpeed * Time.deltaTime
            );
        }
        else
        {
            // Snap to exact value once close enough — avoids endless micro-lerp
            GhostFill.fillAmount = HPFill.fillAmount;
        }
    }

    #endregion                  //----------------------------------------


    #region Event Handlers      //----------------------------------------

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        _cachedMaxHP = maxHealth > 0f ? maxHealth : 1f;
        float fillRatio = currentHealth / _cachedMaxHP;

        // Update HP fill and color
        if (HPFill != null)
        {
            HPFill.fillAmount = fillRatio;
            HPFill.color = GetHPColor(fillRatio);
        }

        // Update text label — show as a whole number (no decimals on the HUD)
        if (HPText != null)
            HPText.text = Mathf.CeilToInt(currentHealth).ToString();

        // Ghost bar: restart the delay so rapid hits extend the ghost display
        if (GhostFill != null)
        {
            // Only set the ghost target if this hit took the HP bar below the ghost
            // (i.e. this was damage, not a heal). Heals should not flash the ghost.
            if (fillRatio < GhostFill.fillAmount)
            {
                // Restart the delay coroutine — ghost holds at its current position
                if (_ghostDelayCoroutine != null)
                    StopCoroutine(_ghostDelayCoroutine);

                _ghostWaiting = true;
                _ghostDelayCoroutine = StartCoroutine(GhostDelayRoutine());
            }
            else
            {
                // Heal — snap ghost down to HP fill immediately so it doesn't
                // sit above a bar that just went up
                GhostFill.fillAmount = fillRatio;
                GhostFill.color = GetGhostColor(fillRatio);
            }
        }
    }


    private void HandleShieldChanged(float currentShield)
    {
        if (ShieldFill == null || _cachedMaxHP <= 0f) return;

        // Shield overlay fills proportional to currentShield / maxHealth.
        // A shield equal to maxHealth = full overlay, clamped to 1.
        float fillRatio = currentShield / _cachedMaxHP;
        ShieldFill.fillAmount = Mathf.Clamp01(fillRatio);
    }

    private void UpdateMaxHealth(float newValue)
    {
        MaxHPText.text = newValue.ToString();
    }


    //private void HandleGameStateChanged(GameState newState)
    //{
    //    // Visible during active gameplay and while paused — hidden on all other states
    //    bool shouldShow = newState == GameState.Playing || newState == GameState.Paused;
    //    gameObject.SetActive(shouldShow);
    //}

    #endregion                  //----------------------------------------


    #region Helpers             //----------------------------------------

    // Returns the correct HP bar color based on fill ratio thresholds
    private Color GetHPColor(float fillRatio)
    {
        if (fillRatio <= 0.3f)
            return ColorDanger;
        if (fillRatio <= 0.6f)
            return ColorCaution;
        return ColorHealthy;
    }

    private Color GetGhostColor(float fillRatio)
    {
        // Ghost bar is a semi-transparent version of the HP color
        Color baseColor = GetHPColor(fillRatio);
        return new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
    }


    // Reads the Guardian's current health from the component and forces a full UI refresh.
    // Called from OnEnable so the bar is correct the moment it becomes visible.
    private void RefreshFromCurrentHealth()
    {
        if (_healthComponent == null) return;

        HandleHealthChanged(_healthComponent.CurrentHealth, _healthComponent.GetMaxHealth());
        HandleShieldChanged(_healthComponent.CurrentShield);
        UpdateMaxHealth(_healthComponent.GetMaxHealth());
    }


    // Holds the ghost bar in place for GhostDelaySeconds, then releases it
    // to start lerping toward HPFill in Update().
    private IEnumerator GhostDelayRoutine()
    {
        _ghostWaiting = true;
        yield return new WaitForSeconds(GhostDelaySeconds);
        _ghostWaiting = false;
    }

    #endregion                  //----------------------------------------
}