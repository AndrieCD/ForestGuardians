// Mb_PanoharraManager.cs
// Sits on the Panoharra Tree GameObject alongside Mb_StatBlock and Mb_HealthComponent.
//
// The Panoharra Tree is the stage's central defense objective.
// CuBots already navigate toward GameObjects tagged "Panoharra" and deal damage
// via I_Damageable.TakeDamage() — this manager wires health up correctly and fires
// a static event when the tree dies so the defeat system can react without a direct
// reference to this object.
//
// WHY Mb_StatBlock + Mb_HealthComponent INSTEAD OF A CUSTOM HEALTH CLASS:
//   Mb_HealthComponent already handles health clamping, shields, and the OnDeath event.
//   Reusing it keeps the Panoharra consistent with every other damageable object in
//   the project — the defeat manager subscribes to one familiar event pattern.
//
// INSPECTOR SETUP:
//   1. Add Mb_StatBlock, Mb_HealthComponent, and Mb_PanoharraManager to the Panoharra GameObject.
//   2. Tag the Panoharra GameObject "Panoharra" in the Inspector (so CuBots can find it).
//   3. Create an SO_CuBots asset named "Panoharra_SO":
//        - Set MaxHealth to your desired value (default 1000f).
//        - Leave every other field at 0 — the Panoharra does not move, attack, or regen.
//   4. Assign that "Panoharra_SO" asset to the PanoharraTemplate field here.

using System;
using UnityEngine;

public class Mb_PanoharraManager : MonoBehaviour
{

    #region Inspector Fields            //----------------------------------------

    [Header("Template")]
    [SerializeField] private SO_Panoharra _PanoharraTemplate;

    #endregion                          //----------------------------------------


    #region Component References        //----------------------------------------

    private Mb_StatBlock _stats;
    private Mb_HealthComponent _health;

    #endregion                          //----------------------------------------


    #region Static Event                //----------------------------------------

    /// <summary>
    /// Fired the moment the Panoharra's health reaches zero.
    /// Mb_DefeatManager subscribes to this to start the defeat sequence.
    /// Static so no direct scene reference to this object is required.
    /// </summary>
    public static event Action OnPanoharraDestroyed;

    // Fired when the Panoharra takes damage, gated by a cooldown so it
    // doesn't spam every hit. Mb_TopBarUI subscribes to trigger audio and blink.
    public static event Action OnPanoharraUnderAttack;

    #endregion                          //----------------------------------------


    #region Pass-Through Properties     //----------------------------------------

    // These forward health values to the HUD health bar without exposing the
    // full Mb_HealthComponent or Mb_StatBlock components publicly.

    /// <summary>The Panoharra's current health. Read by the HUD health bar.</summary>
    public float CurrentHealth => _health != null ? _health.CurrentHealth : 0f;

    /// <summary>The Panoharra's maximum health. Read by the HUD health bar.</summary>
    public float MaxHealth => _stats != null ? _stats.MaxHealth.GetValue() : 0f;

    private float _attackWarningCooldown = 0f;

    [Header("Attack Warning")]
    [Tooltip("Minimum seconds between 'under attack' warning triggers. Default 10f.")]
    [SerializeField] private float AttackWarningInterval = 10f;

    #endregion                          //----------------------------------------


    #region Unity Lifecycle             //----------------------------------------

    private void Awake()
    {
        _stats = GetComponent<Mb_StatBlock>();
        _health = GetComponent<Mb_HealthComponent>();

        if (_stats == null)
            Debug.LogError("[Mb_PanoharraManager] Missing Mb_StatBlock on the Panoharra GameObject.");

        if (_health == null)
            Debug.LogError("[Mb_PanoharraManager] Missing Mb_HealthComponent on the Panoharra GameObject.");

        if (_PanoharraTemplate == null)
        {
            Debug.LogError("[Mb_PanoharraManager] No SO_CuBots template assigned. " +
                           "Create an SO_CuBots asset named 'Panoharra_SO' and assign it in the Inspector.");
            return;
        }

        // Build stats from the template SO.
        // Only MaxHealth is meaningful here — all other fields should be 0 on the SO.
        _stats.BuildFromTemplate(_PanoharraTemplate);

        // Initialize health AFTER stats are built so Mb_HealthComponent can
        // correctly read MaxHealth when setting CurrentHealth to full.
        _health.Initialize();
    }


    private void OnEnable()
    {
        if (_health != null)
            _health.OnDeath += HandleDeath;

        // Subscribe to damage so we can fire the warning event
        if (_health != null)
            _health.OnDamageTaken += HandleDamageTaken;
    }


    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDeath -= HandleDeath;
            _health.OnDamageTaken -= HandleDamageTaken;
        }
    }


    private void Update()
    {
        if (_attackWarningCooldown > 0f)
            _attackWarningCooldown -= Time.deltaTime;
    }



    #endregion                          //----------------------------------------


    #region Event Handler               //----------------------------------------
    private void HandleDamageTaken(float amount)
    {
        if (_attackWarningCooldown > 0f) return;

        _attackWarningCooldown = AttackWarningInterval;
        OnPanoharraUnderAttack?.Invoke();
    }

    private void HandleDeath()
    {
        // Notify the defeat manager (and any future listener) without a direct reference.
        // The defeat manager's OnEnable() subscribes to this static event.
        OnPanoharraDestroyed?.Invoke();
    }

    #endregion                          //----------------------------------------
}
