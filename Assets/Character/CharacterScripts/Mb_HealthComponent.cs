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

    // HealthComponent needs StatBlock to know the MaxHealth cap when healing
    private Mb_StatBlock _statBlock;


    #region Runtime State   //----------------------------------------

    public float CurrentHealth { get; private set; }
    private List<Sc_ShieldInstance> _shields = new List<Sc_ShieldInstance>();

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

    #endregion              //----------------------------------------


    #region Events          //----------------------------------------

    // UI, sound, and VFX should subscribe to these instead of polling CurrentHealth
    public event Action<float, float> OnHealthChanged;  // (currentHealth, maxHealth)
    public event Action<float> OnShieldChanged;  // (currentShield
    public event Action OnDeath;

    #endregion              //----------------------------------------


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
        CurrentHealth = _statBlock.MaxHealth.GetValue();

        // Invoke OnChanged events to initialize UI and other listeners with the correct starting values
        OnHealthChanged?.Invoke(CurrentHealth, _statBlock.MaxHealth.GetValue());
        OnShieldChanged?.Invoke(CurrentShield);
    }


    #region I_Damageable            //----------------------------------------

    /// <summary>
    /// Applies damage to this character. Ignored if already dead.
    /// Triggers OnDeath if health reaches zero.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        float remainingDamage = AbsorbWithShields(amount);

        if (remainingDamage > 0)
        {
            CurrentHealth -= remainingDamage;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);

            OnHealthChanged?.Invoke(CurrentHealth, _statBlock.MaxHealth.GetValue());
        }

        Debug.Log($"[{gameObject.name}] took {amount} damage. Remaining HP: {CurrentHealth}");

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
        IsDead = true;
        Debug.Log($"[{gameObject.name}] has died.");
        OnDeath?.Invoke();
    }



    // SHIELD API
    public void AddShield(float amount, float duration)
    {
        if (amount <= 0) return;

        var shield = new Sc_ShieldInstance(amount, duration);
        _shields.Add(shield);

        if (duration != float.PositiveInfinity)
            StartCoroutine(HandleShieldDuration(shield));

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
}