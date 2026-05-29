// Mb_TopBarUI.cs
// Drives the top bar of the HUD: Panoharra healthbar, wave progress, and phase label.
//
// HOW IT WORKS:
//   - PanoharraHealthObject: drag the child GameObject that has Mb_HealthComponent
//     (the "smaller Panoharra empty object with all scripts") — NOT the prefab root.
//   - Subscribes to Mb_HealthComponent events for the Panoharra HP bar.
//   - Subscribes to Mb_WaveManager static events for wave tracking and phase changes.
//   - Wave progress fill = enemiesKilled / totalEnemiesThisWave.
//     totalEnemiesThisWave is counted via OnEnemySpawned; reset on OnWaveStart.
//   - On wave clear (OnWaveEnd), fill lerps to 1 before resetting for the next wave.
//   - PhaseLabel shows context-sensitive text per phase:
//       Preparation : "NEXT WAVE IN X"  (X counts down from PreparationDuration)
//       Combat      : "WAVE X"          (no timer)
//       Resolution  : "WAVE X COMPLETE" (no timer)
//
// HIERARCHY (set up in the Inspector / Unity Editor):
//   TopBar (this GameObject)
//   ├── PanoharraBar
//   │   ├── PanoharraFill    (Image — fillMethod Horizontal)
//   │   └── PanoharraHPText  (TMP_Text)
//   ├── WaveProgress
//   │   └── WaveProgressFill (Image — fillMethod Horizontal)
//   └── PhaseLabel           (TMP_Text — single shared label for all phase text)
//
// Inspector Setup:
//   - PanoharraHealthObject: the child GO that owns Mb_HealthComponent on the Panoharra prefab.
//   - PanoharraFill, PanoharraHPText: Panoharra bar UI refs.
//   - WaveProgressFill: wave progress bar ref.
//   - PhaseLabel: single TMP_Text that shows phase-contextual text.
//   - WaveClearLerpSpeed: how fast fill completes to 1 on wave clear (default 4.0).
//   - WaveClearHoldSeconds: how long the bar sits at 1 before resetting (default 0.8).

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_TopBarUI : MonoBehaviour
{
    #region Inspector Fields        //----------------------------------------

    [Header("Panoharra Bar")]
    [Tooltip("Drag the child GameObject on the Panoharra prefab that has Mb_HealthComponent attached.")]
    [SerializeField] private GameObject PanoharraHealthObject;

    [Tooltip("Horizontal fill Image for Panoharra HP.")]
    [SerializeField] private Image PanoharraFill;

    [Tooltip("TMP_Text label showing Panoharra's current HP as a whole number.")]
    [SerializeField] private TMP_Text PanoharraHPText;

    [Header("Wave Progress")]
    [Tooltip("Horizontal fill Image tracking enemies killed this wave.")]
    [SerializeField] private Image WaveProgressFill;

    [Header("Phase Label")]
    [Tooltip("Single TMP_Text label used for all phase-contextual text. " +
             "Shows 'NEXT WAVE IN X' during preparation, 'WAVE X' during combat, " +
             "and 'WAVE X COMPLETE' during resolution.")]
    [SerializeField] private TMP_Text PhaseLabel;

    [Header("Wave Clear Animation")]
    [Tooltip("Speed at which the wave progress bar fills to 1 on wave clear.")]
    [SerializeField] private float WaveClearLerpSpeed = 4.0f;

    [Tooltip("Seconds the bar holds at full before resetting for the next wave.")]
    [SerializeField] private float WaveClearHoldSeconds = 0.8f;

    #endregion                      //----------------------------------------


    #region Private State           //----------------------------------------

    private Mb_HealthComponent _panoharraHealth;
    private float _cachedPanoharraMaxHP = 1f;

    // Wave enemy tracking
    private int _totalEnemiesThisWave = 0;
    private int _enemiesKilledThisWave = 0;

    // Coroutine handle for the wave-clear fill animation
    private Coroutine _waveClearCoroutine;
    private bool _waveClearAnimating = false;

    // Cached so resolution and preparation labels can reference the correct number
    private int _currentWaveIndex = 0;

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        
    }

    private void Start()
    {
        if (PanoharraHealthObject != null)
            _panoharraHealth = PanoharraHealthObject.GetComponent<Mb_HealthComponent>();

        if (_panoharraHealth == null)
            Debug.LogError("[Mb_TopBarUI] Could not find Mb_HealthComponent on PanoharraHealthObject. " +
                           "Make sure you dragged the child GO with the scripts, not the prefab root.");

        if (PanoharraFill == null)
            Debug.LogError("[Mb_TopBarUI] PanoharraFill Image is not assigned.");

        if (WaveProgressFill == null)
            Debug.LogError("[Mb_TopBarUI] WaveProgressFill Image is not assigned.");

        if (PhaseLabel == null)
            Debug.LogError("[Mb_TopBarUI] PhaseLabel TMP_Text is not assigned.");

        if (WaveProgressFill != null)
            WaveProgressFill.fillAmount = 0f;

        // Also subscribe to health here now that the ref is ready —
        // OnEnable fired before Start() so the subscription was skipped the first time
        if (_panoharraHealth != null)
        {
            _panoharraHealth.OnHealthChanged -= HandlePanoharraHealthChanged;
            _panoharraHealth.OnHealthChanged += HandlePanoharraHealthChanged;
            RefreshPanoharraBar();
        }
    }

    private void OnEnable()
    {
        // _panoharraHealth may be null here on the first enable (Start() hasn't run yet)
        // — the subscription is completed in Start() once the ref is ready
        if (_panoharraHealth != null)
        {
            _panoharraHealth.OnHealthChanged -= HandlePanoharraHealthChanged;
            _panoharraHealth.OnHealthChanged += HandlePanoharraHealthChanged;
        }

        Mb_WaveManager.OnWaveStart -= HandleWaveStart;
        Mb_WaveManager.OnWaveStart += HandleWaveStart;
        Mb_WaveManager.OnPhaseChanged -= HandlePhaseChanged;
        Mb_WaveManager.OnPhaseChanged += HandlePhaseChanged;
        Mb_WaveManager.OnPreparationTick -= HandlePreparationTick;
        Mb_WaveManager.OnPreparationTick += HandlePreparationTick;
        Mb_WaveManager.OnEnemySpawned -= HandleEnemySpawned;
        Mb_WaveManager.OnEnemySpawned += HandleEnemySpawned;
        Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;
        Mb_WaveManager.OnWaveEnd += HandleWaveEnd;
        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;
        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath;

        RefreshPanoharraBar();
    }


    private void OnDisable()
    {
        if (_panoharraHealth != null)
            _panoharraHealth.OnHealthChanged -= HandlePanoharraHealthChanged;

        Mb_WaveManager.OnPhaseChanged -= HandlePhaseChanged;
        Mb_WaveManager.OnPreparationTick -= HandlePreparationTick;
        Mb_WaveManager.OnWaveStart -= HandleWaveStart;
        Mb_WaveManager.OnEnemySpawned -= HandleEnemySpawned;
        Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;
        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;
    }

    #endregion                      //----------------------------------------


    #region Panoharra Health        //----------------------------------------

    private void HandlePanoharraHealthChanged(float currentHP, float maxHP)
    {
        _cachedPanoharraMaxHP = maxHP > 0f ? maxHP : 1f;

        if (PanoharraFill != null)
            PanoharraFill.fillAmount = Mathf.Clamp01(currentHP / _cachedPanoharraMaxHP);

        if (PanoharraHPText != null)
            PanoharraHPText.text = Mathf.CeilToInt(currentHP).ToString();
    }


    private void RefreshPanoharraBar()
    {
        if (_panoharraHealth == null) return;
        HandlePanoharraHealthChanged(
            _panoharraHealth.CurrentHealth,
            _panoharraHealth.GetMaxHealth()
        );
    }

    #endregion                      //----------------------------------------


    #region Phase Label             //----------------------------------------

    private void HandlePhaseChanged(WavePhase phase)
    {
        Debug.Log($"[TopBarUI] HandlePhaseChanged: phase={phase}, _currentWaveIndex={_currentWaveIndex}");

        switch (phase)
        {
            case WavePhase.Preparation:
                SetPhaseLabel("NEXT WAVE IN ...");
                break;
            case WavePhase.Combat:
                SetPhaseLabel($"WAVE {_currentWaveIndex + 1}");
                break;
            case WavePhase.Resolution:
                SetPhaseLabel($"WAVE {_currentWaveIndex + 1} COMPLETE");
                break;
        }
    }


    private void HandlePreparationTick(float remaining)
    {
        // Round up so the display reads "10" at the start, not "9"
        SetPhaseLabel($"NEXT WAVE IN {Mathf.CeilToInt(remaining)}");
    }


    private void SetPhaseLabel(string text)
    {
        if (PhaseLabel != null)
            PhaseLabel.text = text;
    }

    #endregion                      //----------------------------------------


    #region Wave Progress           //----------------------------------------

    private void HandleWaveStart(int waveIndex)
    {
        _currentWaveIndex = waveIndex;
        Debug.Log($"[TopBarUI] HandleWaveStart: index={waveIndex}, label will show WAVE {waveIndex + 1}");

        // Update the label directly here — don't rely solely on HandlePhaseChanged
        // to carry the correct wave number. Both should agree, but this guarantees
        // the label is correct the moment the wave index is known.
        SetPhaseLabel($"WAVE {_currentWaveIndex + 1}");

        if (_waveClearCoroutine != null)
        {
            StopCoroutine(_waveClearCoroutine);
            _waveClearCoroutine = null;
        }

        _waveClearAnimating = false;
        _totalEnemiesThisWave = 0;
        _enemiesKilledThisWave = 0;

        if (WaveProgressFill != null)
            WaveProgressFill.fillAmount = 0f;
    }


    private void HandleEnemySpawned(GameObject enemy)
    {
        _totalEnemiesThisWave++;
        RefreshWaveProgressFill();
    }


    private void HandleCuBotDeath(GameObject deadEnemy)
    {
        if (_waveClearAnimating) return;

        _enemiesKilledThisWave++;
        RefreshWaveProgressFill();
    }


    private void HandleWaveEnd(int waveIndex)
    {
        if (_waveClearCoroutine != null)
            StopCoroutine(_waveClearCoroutine);

        _waveClearCoroutine = StartCoroutine(WaveClearRoutine());
    }


    private void RefreshWaveProgressFill()
    {
        if (WaveProgressFill == null) return;
        if (_totalEnemiesThisWave <= 0) return;

        float fillRatio = (float)_enemiesKilledThisWave / _totalEnemiesThisWave;
        WaveProgressFill.fillAmount = Mathf.Clamp01(fillRatio);
    }


    private IEnumerator WaveClearRoutine()
    {
        _waveClearAnimating = true;

        // Lerp fill to full
        while (WaveProgressFill.fillAmount < 0.999f)
        {
            WaveProgressFill.fillAmount = Mathf.Lerp(
                WaveProgressFill.fillAmount,
                1f,
                WaveClearLerpSpeed * Time.deltaTime
            );
            yield return null;
        }

        // Snap to exactly 1 and hold
        WaveProgressFill.fillAmount = 1f;
        yield return new WaitForSeconds(WaveClearHoldSeconds);

        // Reset to empty — ready for next wave
        WaveProgressFill.fillAmount = 0f;
        _waveClearAnimating = false;
        _waveClearCoroutine = null;
    }

    #endregion                      //----------------------------------------
}