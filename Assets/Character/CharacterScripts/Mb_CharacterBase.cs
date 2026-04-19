using System;
using UnityEngine;

/// <summary>
/// The root base class for all characters — Guardias and CuBots.
/// 
/// This class is purely an abstract hub: it holds references to the separate
/// components that handle each responsibility (Stats, Health, Abilities,
/// Movement). It doesn't do any of that work itself.
///
/// Derived classes: Mb_GuardianBase, MB_CuBotBase
/// </summary>
public abstract class Mb_CharacterBase : MonoBehaviour
{

    [Header("Identity")]
    protected string _CharacterName;
    protected int _CharacterLevel = 1;  // start level 1, reduce by 1 for array indexing
    protected int _MaxLevel = 15;     // max level 15, reduce by 1 for array indexing

    // Component references — fetched once in Awake, used everywhere
    public Mb_StatBlock Stats { get; private set; }
    public Mb_HealthComponent Health { get; private set; }
    public Mb_AbilityController Abilities { get; private set; }
    public Mb_Movement Movement { get; private set; }

    #region EVENTS
    public event Action<int> OnLevelUp;
    #endregion


    protected virtual void Awake()
    {
        // Grab all sibling components that were added to this GameObject in the Inspector
        Stats = GetComponent<Mb_StatBlock>();
        Health = GetComponent<Mb_HealthComponent>();
        Abilities = GetComponent<Mb_AbilityController>();
        Movement = GetComponent<Mb_Movement>();

        // Null checks so errors are caught early with a clear message
        if (Stats == null) Debug.LogError($"[Mb_CharacterBase] Missing Mb_StatBlock on {gameObject.name}");
        if (Health == null) Debug.LogError($"[Mb_CharacterBase] Missing Mb_HealthComponent on {gameObject.name}");
        if (Abilities == null) Debug.LogError($"[Mb_CharacterBase] Missing Mb_AbilityController on {gameObject.name}");

        InitializeFromTemplate();
    }


    /// <summary>
    /// Called at the end of Awake. Each derived class uses this to populate
    /// Stats and Health from their ScriptableObject template.
    /// </summary>
    protected abstract void InitializeFromTemplate();


    public void LevelUp()
    {
        if (_CharacterLevel >= _MaxLevel)
        {
            Debug.LogWarning($"[{_CharacterName}] Already at max level {_MaxLevel}.");
            return;
        }

        _CharacterLevel++;
        Debug.Log($"[{_CharacterName}] Leveled up to {_CharacterLevel}!");

        // SetLevel returns the MaxHealth increase so we can heal the character by
        // that exact amount — their HP stays "full relative to the new max" on level-up.
        float maxHPDelta = Stats.SetLevel(_CharacterLevel);
        Health.Heal(maxHPDelta);

        OnLevelUp?.Invoke(_CharacterLevel);
    }



}