// Mb_CuBotHealthBar.cs
// World-space health bar displayed above each CuBot.
//
// HOW IT WORKS:
//   - Attach this script to the same prefab root as MB_CuBotBase.
//   - Place a World Space Canvas as a child of the prefab. Assign all UI refs in Inspector.
//   - This script subscribes to the CuBot's own Mb_HealthComponent events to
//     update the health and shield fill bars.
//   - LateUpdate keeps the canvas billboard-facing the main camera every frame.
//   - OnEnable() resets the bar to full and makes it visible again (pool reuse safe).
//   - OnDisable() hides the bar — CuBot death calls SetActive(false) on the GameObject,
//     which automatically triggers OnDisable here.
//
// Inspector setup:
//   - Assign HealthBarCanvas: the World Space Canvas child on this prefab
//   - Assign HealthFillImage: the Image component that represents current HP fill
//   - Assign ShieldFillImage: the Image component layered on top for shield fill
//   - Assign NameLabel: the TMP_Text label that shows the CuBot's name
//   - HeightOffset: vertical world-units above the model's pivot (default 2.5)
//   - BarSize: width and height of the health bar in world-space units (default 1.5, 0.15)

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_CuBotHealthBar : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Header("UI References")]
    [SerializeField] private Canvas _HealthBarCanvas;
    [SerializeField] private Image _HealthFillImage;
    [SerializeField] private Image _ShieldFillImage;
    [SerializeField] private TMP_Text _NameLabel;

    [Header("Layout")]
    [Tooltip("How far above the CuBot's pivot the bar floats, in world units.")]
    [SerializeField] private float HeightOffset = 2.5f;

    [Tooltip("Width and height of the health bar in world-space units.")]
    [SerializeField] private Vector2 BarSize = new Vector2(1.5f, 0.15f);

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    // Cached reference to this CuBot's health component
    private Mb_HealthComponent _health;

    // Cached camera reference — fetched once in Awake, never per-frame
    private Camera _mainCamera;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        _mainCamera = Camera.main;
        _health = GetComponent<Mb_HealthComponent>();

        if (_health == null)
            Debug.LogError($"[Mb_CuBotHealthBar] No Mb_HealthComponent found on {gameObject.name}.");

        if (_HealthBarCanvas == null)
            Debug.LogError($"[Mb_CuBotHealthBar] HealthBarCanvas is not assigned on {gameObject.name}.");

        // Apply the configured bar size to the canvas's RectTransform
        if (_HealthBarCanvas != null)
        {
            RectTransform canvasRect = _HealthBarCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
                canvasRect.sizeDelta = BarSize;
        }
    }


    private void OnEnable()
    {
        // Always subscribe fresh on enable — avoids stacking duplicate listeners
        // across pool reuses (unsubscribe first as a safety net)
        if (_health != null)
        {
            _health.OnHealthChanged -= HandleHealthChanged;
            _health.OnHealthChanged += HandleHealthChanged;

            _health.OnShieldChanged -= HandleShieldChanged;
            _health.OnShieldChanged += HandleShieldChanged;

            _health.OnDeath -= HandleDeath;
            _health.OnDeath += HandleDeath;
        }

        // Reposition the canvas at the correct height above the pivot
        if (_HealthBarCanvas != null)
        {
            _HealthBarCanvas.transform.localPosition = new Vector3(0f, HeightOffset, 0f);
            _HealthBarCanvas.gameObject.SetActive(true);
        }

        // Read the name directly from the CuBot component — it's populated in Awake
        // before OnEnable fires, so _CharacterName is always ready here
        MB_CuBotBase cuBot = GetComponent<MB_CuBotBase>();
        if (_NameLabel != null && cuBot != null)
        {
            // _CharacterName is protected, so we read it via the public GetLevel() sibling
            // pattern — but _CharacterName has no public accessor. We'll grab it from the
            // GameObject name as a fallback, or you can add a public property to Mb_CharacterBase.
            // TODO: Add `public string CharacterName => _CharacterName;` to Mb_CharacterBase
            //       so this reads the clean SO name instead of the prefab instance name.
            _NameLabel.text = cuBot.CharacterName   ;
        }

        // Reset both bars to full so a pool-reused CuBot starts looking healthy
        ResetBars();
    }


    private void OnDisable()
    {
        // Unsubscribe when this GameObject is deactivated (death / pool return)
        if (_health != null)
        {
            _health.OnHealthChanged -= HandleHealthChanged;
            _health.OnShieldChanged -= HandleShieldChanged;
            _health.OnDeath -= HandleDeath;
        }
    }


    private void LateUpdate()
    {
        // Billboard — rotate the canvas to face the camera every frame.
        // LateUpdate is used here so we run AFTER the camera has finished moving,
        // which eliminates the one-frame lag you'd get in Update.
        if (_HealthBarCanvas == null || _mainCamera == null) return;

        _HealthBarCanvas.transform.rotation = _mainCamera.transform.rotation;
    }

    #endregion                  //----------------------------------------


    #region Event Handlers      //----------------------------------------

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (_HealthFillImage == null) return;

        // fillAmount is 0–1, so dividing current by max gives the correct proportion
        float fillRatio = maxHealth > 0f ? currentHealth / maxHealth : 0f;
        _HealthFillImage.fillAmount = fillRatio;
    }


    private void HandleShieldChanged(float currentShield)
    {
        if (_ShieldFillImage == null || _health == null) return;

        // Shield is shown as a proportion of MaxHealth so it lines up visually
        // with the health bar beneath it. A shield equal to MaxHealth = full overlay.
        float maxHealth = _health.CurrentHealth + currentShield;

        // Guard: avoid division by zero if somehow both are 0
        float fillRatio = maxHealth > 0f ? currentShield / _health.CurrentHealth : 0f;

        // Clamp to 1 — shield can technically exceed max HP in this system
        _ShieldFillImage.fillAmount = Mathf.Clamp01(fillRatio);
    }


    private void HandleDeath()
    {
        // Hide the bar the instant the CuBot dies.
        // The GameObject will be deactivated shortly after by MB_CuBotBase.HandleDeath(),
        // but we hide immediately so there's no visual artifact between death and deactivation.
        if (_HealthBarCanvas != null)
            _HealthBarCanvas.gameObject.SetActive(false);
    }

    #endregion                  //----------------------------------------


    #region Helpers             //----------------------------------------

    // Resets both fill bars to 1 (full) — called on pool reuse in OnEnable
    private void ResetBars()
    {
        if (_HealthFillImage != null)
            _HealthFillImage.fillAmount = 1f;

        if (_ShieldFillImage != null)
            _ShieldFillImage.fillAmount = 0f; // No shield on fresh spawn
    }

    #endregion                  //----------------------------------------
}