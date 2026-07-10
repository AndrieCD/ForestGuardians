// Mb_GuardianHealthbarUI.cs
// Replaces the Mb_Healthbar.cs prototype.
// Drives the Guardian's health and shield bar on the HUD Canvas.
// Also manages low HP feedback: heartbeat SFX loop and camera vignette.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_GuardianHealthbarUI : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Header("Bar Images")]
    [SerializeField] private Image HPFill;
    [SerializeField] private Image GhostFill;
    [SerializeField] private Image ShieldFill;

    [Header("Text")]
    [SerializeField] private TMP_Text HPText;
    [SerializeField] private TMP_Text MaxHPText;

    [Header("Ghost Bar Tuning")]
    [SerializeField] private float GhostLerpSpeed = 3.0f;
    [SerializeField] private float GhostDelaySeconds = 0.6f;

    [Header("Color Thresholds")]
    [SerializeField] private Color ColorHealthy = Color.green;
    [SerializeField] private Color ColorCaution = Color.yellow;
    [SerializeField] private Color ColorDanger = Color.red;

    [Header("Shield Fill Color")]
    [SerializeField] private Color ShieldColor = new Color(0.4f, 0.7f, 1f, 0.85f);


    [Header("Low HP Feedback")]
    [Tooltip("HP fraction below which heartbeat and vignette activate.")]
    [SerializeField] private float LowHPThreshold = 0.45f;

    [Tooltip("HP fraction below which vignette reaches maximum intensity.")]
    [SerializeField] private float CriticalHPThreshold = 0.15f;

    [Tooltip("Seconds between each heartbeat SFX pulse. Default 1.5f.")]
    [SerializeField] private float HeartbeatInterval = 0.5f;

    [Tooltip("The Q_Vignette_Single component on the HUD Canvas. " +
             "Drag the vignette prefab root here.")]
    [SerializeField] private Q_Vignette_Single LowHPVignette;

    [Tooltip("Vignette scale at exactly LowHPThreshold (35% HP). Default 1.0f.")]
    [SerializeField] private float VignetteScaleAtLowHP = 1.0f;

    [Tooltip("Vignette scale at CriticalHPThreshold and below (15% HP). Default 2.0f.")]
    [SerializeField] private float VignetteScaleAtCritical = 2.0f;

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    private Mb_HealthComponent _healthComponent;
    private Mb_StatBlock _statBlock;

    private float _cachedMaxHP = 1f;
    private float _ghostFillTarget = 1f;
    private Coroutine _ghostDelayCoroutine;
    private bool _ghostWaiting = false;

    // Low HP state
    private bool _isLowHP = false;
    private Coroutine _heartbeatCoroutine;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Start()
    {
        if (ShieldFill != null)
            ShieldFill.color = ShieldColor;

    }


    private void OnEnable()
    {
        Mb_GuardianBase.OnActiveGuardianChanged -= HandleActiveGuardianChanged;
        Mb_GuardianBase.OnActiveGuardianChanged += HandleActiveGuardianChanged;

        BindGuardian(Mb_GuardianBase.CurrentGuardian);
    }


    private void OnDisable()
    {
        Mb_GuardianBase.OnActiveGuardianChanged -= HandleActiveGuardianChanged;
        UnbindGuardian();

        // Always clean up low HP feedback when the HUD disables
        // (death, scene teardown, defeat screen appearing)
        StopLowHPFeedback();
    }


    private void Update()
    {
        // Ghost bar chase
        if (GhostFill != null && !_ghostWaiting)
        {
            if (Mathf.Abs(GhostFill.fillAmount - HPFill.fillAmount) > 0.001f)
                GhostFill.fillAmount = Mathf.Lerp(GhostFill.fillAmount, HPFill.fillAmount, GhostLerpSpeed * Time.deltaTime);
            else
                GhostFill.fillAmount = HPFill.fillAmount;
        }

        // Vignette scale smooth update
        if (_isLowHP && _healthComponent != null)
        {
            float fillRatio = _healthComponent.CurrentHealth / _cachedMaxHP;
            UpdateVignetteScale(fillRatio);
        }
    }

    #endregion                  //----------------------------------------


    #region Guardian Binding    //----------------------------------------

    private void HandleActiveGuardianChanged(Mb_GuardianBase guardian)
    {
        BindGuardian(guardian);
    }


    private void BindGuardian(Mb_GuardianBase guardian)
    {
        UnbindGuardian();

        if (guardian == null)
            return;

        _healthComponent = guardian.Health;
        _statBlock = guardian.Stats;

        if (_healthComponent != null)
        {
            _healthComponent.OnHealthChanged += HandleHealthChanged;
            _healthComponent.OnShieldChanged += HandleShieldChanged;
        }
        else
        {
            Debug.LogError($"[Mb_GuardianHealthbarUI] No Mb_HealthComponent found on {guardian.gameObject.name}.");
        }

        if (_statBlock != null)
            _statBlock.MaxHealth.OnStatChanged += UpdateMaxHealth;
        else
            Debug.LogError($"[Mb_GuardianHealthbarUI] No Mb_StatBlock found on {guardian.gameObject.name}.");

        RefreshFromCurrentHealth();
    }


    private void UnbindGuardian()
    {
        if (_healthComponent != null)
        {
            _healthComponent.OnHealthChanged -= HandleHealthChanged;
            _healthComponent.OnShieldChanged -= HandleShieldChanged;
        }

        if (_statBlock != null)
            _statBlock.MaxHealth.OnStatChanged -= UpdateMaxHealth;

        _healthComponent = null;
        _statBlock = null;
    }

    #endregion                  //----------------------------------------


    #region Event Handlers      //----------------------------------------

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        _cachedMaxHP = maxHealth > 0f ? maxHealth : 1f;
        float fillRatio = currentHealth / _cachedMaxHP;

        if (HPFill != null)
        {
            HPFill.fillAmount = fillRatio;
            HPFill.color = GetHPColor(fillRatio);
        }

        if (HPText != null)
            HPText.text = Mathf.CeilToInt(currentHealth).ToString();

        if (GhostFill != null)
        {
            if (fillRatio < GhostFill.fillAmount)
            {
                if (_ghostDelayCoroutine != null)
                    StopCoroutine(_ghostDelayCoroutine);

                _ghostWaiting = true;
                _ghostDelayCoroutine = StartCoroutine(GhostDelayRoutine());
            }
            else
            {
                GhostFill.fillAmount = fillRatio;
                GhostFill.color = GetGhostColor(fillRatio);
            }
        }

        // Low HP threshold check
        bool shouldBeLowHP = fillRatio <= LowHPThreshold;

        if (shouldBeLowHP && !_isLowHP)
            StartLowHPFeedback();
        else if (!shouldBeLowHP && _isLowHP)
            StopLowHPFeedback();
    }


    private void HandleShieldChanged(float currentShield)
    {
        if (ShieldFill == null || _cachedMaxHP <= 0f) return;

        float fillRatio = currentShield / _cachedMaxHP;
        ShieldFill.fillAmount = Mathf.Clamp01(fillRatio);
    }


    private void UpdateMaxHealth(float newValue)
    {
        if (MaxHPText != null)
            MaxHPText.text = Mathf.CeilToInt(newValue).ToString();
    }

    #endregion                  //----------------------------------------


    #region Low HP Feedback     //----------------------------------------

    private void StartLowHPFeedback()
    {
        if (_isLowHP) return;
        _isLowHP = true;

        if (_heartbeatCoroutine != null)
            StopCoroutine(_heartbeatCoroutine);
        _heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
    }


    private void StopLowHPFeedback()
    {
        if (!_isLowHP) return;
        _isLowHP = false;

        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }

        // Reset vignette to invisible
        SetVignetteScale(0f);
    }


    // Calculates and applies the correct vignette scale based on current HP fraction.
    // Scale grows continuously as HP drops — no snap at any threshold.
    //
    // Above 30% HP  → 0 (invisible)
    // At 30% HP     → VignetteScaleAtLowHP (default 1.0) — fully grown in
    // At 10% HP     → VignetteScaleAtCritical (default 2.0)
    // Between 10–30% → lerped between the two values
    private void UpdateVignetteScale(float fillRatio)
    {
        if (!_isLowHP)
        {
            SetVignetteScale(0f);
            return;
        }

        float targetScale;

        if (fillRatio <= CriticalHPThreshold)
        {
            // At or below 10% — clamp to max
            targetScale = VignetteScaleAtCritical;
        }
        else
        {
            // Between 30% and 10% — grow from 0 to VignetteScaleAtLowHP as HP drops toward 30%,
            // then continue growing to VignetteScaleAtCritical as it drops toward 10%
            // t = 0 at LowHPThreshold (30%), t = 1 at CriticalHPThreshold (10%)
            float t = Mathf.InverseLerp(LowHPThreshold, CriticalHPThreshold, fillRatio);
            targetScale = Mathf.Lerp(VignetteScaleAtLowHP, VignetteScaleAtCritical, t);
        }

        // Smoothly lerp the actual mainScale toward the target each frame
        // so changes feel gradual rather than frame-instant
        float currentScale = LowHPVignette != null ? LowHPVignette.mainScale : 0f;
        SetVignetteScale(Mathf.Lerp(currentScale, targetScale, Time.deltaTime * 3f));
    }


    private void SetVignetteScale(float scale)
    {
        if (LowHPVignette != null)
            LowHPVignette.mainScale = scale;
    }


    private IEnumerator HeartbeatRoutine()
    {
        while (_isLowHP)
        {
            Mb_AudioManager.PlayUI(UISFX.UX_Heartbeat);
            yield return new WaitForSecondsRealtime(HeartbeatInterval);
        }
    }

    #endregion                  //----------------------------------------


    #region Helpers             //----------------------------------------

    private Color GetHPColor(float fillRatio)
    {
        if (fillRatio <= CriticalHPThreshold) return ColorDanger;
        if (fillRatio <= LowHPThreshold) return ColorCaution;
        return ColorHealthy;
    }

    private Color GetGhostColor(float fillRatio)
    {
        Color baseColor = GetHPColor(fillRatio);
        return new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
    }

    private void RefreshFromCurrentHealth()
    {
        if (_healthComponent == null) return;
        HandleHealthChanged(_healthComponent.CurrentHealth, _healthComponent.GetMaxHealth());
        HandleShieldChanged(_healthComponent.CurrentShield);
        UpdateMaxHealth(_healthComponent.GetMaxHealth());
    }

    private IEnumerator GhostDelayRoutine()
    {
        _ghostWaiting = true;
        yield return new WaitForSeconds(GhostDelaySeconds);
        _ghostWaiting = false;
    }

    #endregion                  //----------------------------------------
}
