using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns a character's health, damage, healing, and death logic.
/// Lives as a component on both Guardian and CuBot GameObjects.
///
/// Other systems (UI, sound, abilities) should listen to the events
/// fired here rather than reading health values directly every frame.
///
/// Requires Mb_StatBlock on the same GameObject to read MaxHealth.
/// </summary>
public class Mb_HealthComponent : MonoBehaviour, I_Damageable
{
    public static bool PlayerGodModeEnabled { get; private set; }


    // HealthComponent needs StatBlock to know the MaxHealth cap when healing
    private Mb_StatBlock _statBlock;
    private Sc_Stat _observedMaxHealth;


    #region Runtime State   //----------------------------------------

    public float CurrentHealth { get; private set; }
    private List<Sc_ShieldInstance> _shields = new List<Sc_ShieldInstance>();
    public bool IsUntargetable = false;
    public bool IsInvulnerable = false;
    public float CurrentShield
    {
        get
        {
            float total = 0;
            foreach (var s in _shields)
                total += s.CurrentValue;
            return total;
        }
    }

    public bool IsDead { get; private set; }

    // Handle to the regen coroutine so we can stop it cleanly on death or pool reset
    private Coroutine _regenCoroutine;

    #endregion              //----------------------------------------


    #region Events          //----------------------------------------

    // UI, sound, and VFX should subscribe to these instead of polling CurrentHealth
    public event Action<float, float> OnHealthChanged;  // (currentHealth, maxHealth)
    public event Action<float> OnMaxHealthChanged; // (newMaxHealth) — fired by StatBlock when MaxHealth changes, e.g. from Heart of the Forest

    public event Action<float> OnDamageTaken; // (damageAmount)
    public event Action<float> OnHealReceived; // (healAmount)
    
    public event Action<float> OnShieldChanged;  // (currentShield
    public event Action<float> OnShieldAdded;       // amount added
    public event Action OnShieldBroken;             // fires when a shield instance hits 0
    public event Action OnDeath;

    #endregion              //----------------------------------------


    private void OnDisable()
    {
        UnsubscribeFromMaxHealth();
        StopRegenCoroutine();
    }


    /// <summary>
    /// Sets CurrentHealth to MaxHealth and clears the dead flag.
    /// Call this after StatBlock.BuildFromTemplate() has run — we need MaxHealth to be ready.
    /// CuBots call this inside Reset(). Guardians call this inside InitializeFromTemplate().
    /// </summary>
    public void Initialize()
    {
        if (_statBlock == null)
            _statBlock = GetComponent<Mb_StatBlock>();

        if (_statBlock == null)
        {
            Debug.LogError($"[Mb_HealthComponent] No Mb_StatBlock found on {gameObject.name}.");
            return;
        }

        IsDead = false;
        _shields.Clear();
        CurrentHealth = _statBlock.MaxHealth.GetValue();

        // Stop any leftover regen coroutine before starting a fresh one.
        // This matters for CuBots returning to the pool and being reactivated.
        StopRegenCoroutine();
        _regenCoroutine = StartCoroutine(RegenLoop());


        // Invoke OnChanged events to initialize UI and other listeners with the correct starting values
        OnHealthChanged?.Invoke(CurrentHealth, _statBlock.MaxHealth.GetValue());
        OnShieldChanged?.Invoke(CurrentShield);

        SubscribeToMaxHealth(_statBlock.MaxHealth);
    }

    private void SubscribeToMaxHealth(Sc_Stat maxHealth)
    {
        if (_observedMaxHealth == maxHealth) return;

        UnsubscribeFromMaxHealth();

        _observedMaxHealth = maxHealth;

        if (_observedMaxHealth != null)
            _observedMaxHealth.OnStatChanged += HandleMaxHealthChanged;
    }

    private void UnsubscribeFromMaxHealth()
    {
        if (_observedMaxHealth == null) return;

        _observedMaxHealth.OnStatChanged -= HandleMaxHealthChanged;
        _observedMaxHealth = null;
    }

    private void HandleMaxHealthChanged(float maxHealth)
    {
        OnMaxHealthChanged?.Invoke(maxHealth);

        // When MaxHealth changes, clamp CurrentHealth if it now exceeds the new cap.
        if (CurrentHealth <= maxHealth) return;

        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }


    #region I_Damageable            //----------------------------------------

    /// <summary>
    /// Applies damage to this character. Ignored if already dead.
    /// Triggers OnDeath if health reaches zero.
    /// </summary>
    public void TakeDamage(float amount, DamageType type = DamageType.Physical)
    {
        if (IsDead) return;
        if (PlayerGodModeEnabled && gameObject.CompareTag("Player")) return;
        if (IsUntargetable || IsInvulnerable) return;

        float remainingDamage = AbsorbWithShields(amount);

        if (remainingDamage > 0)
        {
            CurrentHealth -= remainingDamage;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);

            OnHealthChanged?.Invoke(CurrentHealth, _statBlock.MaxHealth.GetValue());
        }

        OnDamageTaken?.Invoke(amount);


        // Play SFX and VFX
        // different if guardian or cubot
        if (gameObject.CompareTag("Player"))
        {
            // Play sound
            Mb_AudioManager.PlaySFX(CombatSFX.Hit_Guardian);

        }
        else if (gameObject.CompareTag("CuBot"))
        {
            // Play sound
            Mb_AudioManager.PlaySFX(CombatSFX.Hit_CuBot, gameObject.transform.position);
        }

        if (CurrentHealth <= 0f)
            Die();
    }


    /// <summary>
    /// Restores health up to the MaxHealth cap. Ignored if dead.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Min(CurrentHealth + amount, _statBlock.MaxHealth.GetValue());

        OnHealReceived?.Invoke(amount);
        OnHealthChanged?.Invoke(CurrentHealth, _statBlock.MaxHealth.GetValue());
    }


    /// <summary>
    /// Raises current health to a minimum value without firing heal events.
    /// Intended for boss phase gates that prevent threshold skips after burst damage.
    /// </summary>
    public void ClampCurrentHealthMinimum(float minimumHealth)
    {
        if (IsDead || _statBlock == null || _statBlock.MaxHealth == null) return;
        if (CurrentHealth >= minimumHealth) return;

        CurrentHealth = Mathf.Min(minimumHealth, _statBlock.MaxHealth.GetValue());
        OnHealthChanged?.Invoke(CurrentHealth, _statBlock.MaxHealth.GetValue());
    }

    #endregion          //----------------------------------------  


    /// <summary>
    /// Marks the character as dead and fires OnDeath.
    /// The actual response (pool return, game over screen, etc.)
    /// is handled by whoever is subscribed to OnDeath — not here.
    /// </summary>
    private void Die()
    {
        // Play Sound
        // different if guardian or cubot
        if (gameObject.CompareTag("Player"))
        {
            // Play sound
            //Mb_AudioManager.PlaySFX(CombatSFX.);
        }
        else if (gameObject.CompareTag("CuBot"))
        {
            // Play sound
            Mb_AudioManager.PlaySFX(CombatSFX.CuBot_Death, gameObject.transform.position);
        }

        IsDead = true;
        Debug.Log($"[{gameObject.name}] has died.");
        OnDeath?.Invoke();
    }



    #region Health Regeneration     //----------------------------------------

    /// <summary>
    /// Ticks once per second and heals the character by their current HealthRegen value.
    /// Reads GetValue() each tick so augments that modify HealthRegen (e.g. Heart of
    /// the Forest) are automatically reflected without any extra wiring.
    /// Skips the Heal() call if HealthRegen is zero or negative — CuBots with
    /// zero regen incur no cost beyond one yield per second.
    /// </summary>
    private IEnumerator RegenLoop()
    {
        // Use WaitForSeconds(1f) — regen is defined as HP per second in the GDD,
        // and a 1s tick is cheap and predictable. Time.timeScale = 0 (pause) freezes
        // this coroutine automatically, so no pause handling is needed here.
        var wait = new WaitForSeconds(1f);

        while (!IsDead)
        {
            yield return wait;
            if (_statBlock == null || _statBlock.HealthRegen == null) continue;
            float regenAmount = _statBlock.HealthRegen.GetValue();

            // Skip the Heal() call entirely if regen is zero — avoids a
            // pointless Mathf.Min and an OnHealthChanged event fire with no change
            if (regenAmount <= 0f) continue;

            // Skip Heal() if we're already at max health — avoids OnHealthChanged events with no change
            if (CurrentHealth >= _statBlock.MaxHealth.GetValue()) continue;

            Heal(regenAmount);
        }
    }

    private void StopRegenCoroutine()
    {
        if (_regenCoroutine != null)
        {
            StopCoroutine(_regenCoroutine);
            _regenCoroutine = null;
        }
    }

    #endregion                      //----------------------------------------


    #region Shield API              //----------------------------------------

    /// <summary>
    /// Adds a temporary or permanent shield that absorbs incoming damage before health.
    /// </summary>
    public void AddShield(float amount, float duration)
    {
        if (amount <= 0) return;

        var shield = new Sc_ShieldInstance(amount, duration);
        _shields.Add(shield);

        if (duration != float.PositiveInfinity)
            StartCoroutine(HandleShieldDuration(shield));

        OnShieldAdded?.Invoke(amount);
        OnShieldChanged?.Invoke(CurrentShield);
    }

    private IEnumerator HandleShieldDuration(Sc_ShieldInstance shield)
    {
        yield return new WaitForSeconds(shield.Duration);
        _shields.Remove(shield);

        OnShieldChanged?.Invoke(CurrentShield);
    }

    private float AbsorbWithShields(float damage)
    {
        float remainingDamage = damage;

        // Consume shields in order (FIFO — or reverse if you want LIFO behavior)
        for (int i = 0; i < _shields.Count && remainingDamage > 0; i++)
        {
            var shield = _shields[i];

            float absorbed = Mathf.Min(shield.CurrentValue, remainingDamage);
            shield.CurrentValue -= absorbed;
            remainingDamage -= absorbed;

            if (shield.CurrentValue <= 0)
            {
                OnShieldBroken?.Invoke();
                _shields.RemoveAt(i);
                i--;
            }
        }

        OnShieldChanged?.Invoke(CurrentShield);

        return remainingDamage;
    }

    private float GetMaxShield()
    {
        float total = 0;
        foreach (var s in _shields)
            total += s.MaxValue;
        return total;
    }

    #endregion                      //----------------------------------------


    /// <summary>
    /// Returns the current MaxHealth stat value.
    /// </summary>
    public float GetMaxHealth()
    {
        return _statBlock.MaxHealth.GetValue();
    }


    /// <summary>
    /// Enables or disables admin/demo invincibility for the player guardian.
    /// </summary>
    public static void SetPlayerGodModeEnabled(bool enabled)
    {
        PlayerGodModeEnabled = enabled;
        Debug.Log($"[Mb_HealthComponent] Player god mode {(enabled ? "enabled" : "disabled")}.");
    }
}
