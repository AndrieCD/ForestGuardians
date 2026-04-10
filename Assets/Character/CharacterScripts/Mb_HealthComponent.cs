using System;
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
    public bool IsDead { get; private set; }

    #endregion              //----------------------------------------


    #region Events          //----------------------------------------

    // UI, sound, and VFX should subscribe to these instead of polling CurrentHealth
    public event Action<float, float> OnHealthChanged;  // (currentHealth, maxHealth)
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
        CurrentHealth = _statBlock.MaxHealth.Value();
    }


    #region I_Damageable            //----------------------------------------

    /// <summary>
    /// Applies damage to this character. Ignored if already dead.
    /// Triggers OnDeath if health reaches zero.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHealth -= amount;
        CurrentHealth = Mathf.Max(CurrentHealth, 0f); // Clamp so health never goes below zero

        Debug.Log($"[{gameObject.name}] took {amount} damage. Health: {CurrentHealth}/{_statBlock.MaxHealth.Value()}");

        OnHealthChanged?.Invoke(CurrentHealth, _statBlock.MaxHealth.Value());

        if (CurrentHealth <= 0f)
            Die();
    }


    /// <summary>
    /// Restores health up to the MaxHealth cap. Ignored if dead.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Min(CurrentHealth + amount, _statBlock.MaxHealth.Value());

        OnHealthChanged?.Invoke(CurrentHealth, _statBlock.MaxHealth.Value());
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
    
}