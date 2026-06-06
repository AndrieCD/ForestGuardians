// Mb_AbilityController.cs
// Owns all ability slots for a character and controls when they can activate.
// Sits as a component on the Guardian GameObject.
//
// Mb_PlayerController calls ActivateQ(), ActivatePrimary(), etc. from input bindings.
// Mb_GuardianBase calls SetSlots() during InitializeFromTemplate() to wire up abilities.
//
// This is the single place where pause blocks ability use — nothing else needs to change.

using System;
using UnityEngine;

public class Mb_AbilityController : MonoBehaviour
{
    // The character that owns these abilities — passed into every Activate() call
    private Mb_CharacterBase _owner;


    #region Ability Slots       //----------------------------------------

    private Sc_BaseAbility _passiveAbility;
    private Sc_BaseAbility _qAbility;
    private Sc_BaseAbility _eAbility;
    private Sc_BaseAbility _rAbility;
    private Sc_BaseAbility _primaryAttack;
    private Sc_BaseAbility _secondaryAttack;

    #endregion                  //----------------------------------------


    #region State               //----------------------------------------

    private bool _isPaused = false;

    // If true, all ability activation is blocked (e.g. during cutscenes, stun, death)
    public bool IsBlocked =>
        _owner == null ||
        _owner.Health == null ||
        _isPaused ||
        _owner.Health.IsDead;
    #endregion                  //----------------------------------------


    #region Events              //----------------------------------------

    // Fired every time any ability successfully activates (not blocked, not null).
    // Primal Resonance subscribes to this to count casts and add stacks.
    // The string argument is the slot name ("Q", "E", "R", "Primary", "Secondary", "Passive")
    // in case a listener only cares about specific slots.
    public event Action<string> OnAbilityActivated;

    #endregion                  //----------------------------------------


    //private void Awake()
    //{
    //    _owner = GetComponent<Mb_CharacterBase>();

    //    if (_owner == null)
    //        Debug.LogError($"[Mb_AbilityController] No Mb_CharacterBase found on {gameObject.name}.");
    //}


    private void OnEnable()
    {
        Mb_PauseManager.OnPaused += HandlePause;
        Mb_PauseManager.OnResumed += HandleResume;
    }


    private void OnDisable()
    {
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;
    }

    public void Initialize(Mb_CharacterBase owner)
    {
        _owner = owner;

        if (_owner == null)
            Debug.LogError("[Mb_AbilityController] Initialize received null owner.");
    }


    /// <summary>
    /// Assigns all ability slots at once.
    /// Called by Mb_GuardianBase.InitializeFromTemplate() after stats are ready.
    /// Passing null for a slot is allowed — that slot simply won't activate.
    /// </summary>
    public void SetSlots(
        Sc_BaseAbility passive,
        Sc_BaseAbility q,
        Sc_BaseAbility e,
        Sc_BaseAbility r,
        Sc_BaseAbility primary,
        Sc_BaseAbility secondary)
    {
        if (_owner == null)
        {
            Debug.LogError("[Mb_AbilityController] SetSlots called before owner initialization.");
            return;
        }

        _passiveAbility = passive;
        _qAbility = q;
        _eAbility = e;
        _rAbility = r;
        _primaryAttack = primary;
        _secondaryAttack = secondary;

        // Equip all assigned slots — this starts passives and any OnEquip setup
        _passiveAbility?.OnEquip(_owner);
        _qAbility?.OnEquip(_owner);
        _eAbility?.OnEquip(_owner);
        _rAbility?.OnEquip(_owner);
        _primaryAttack?.OnEquip(_owner);
        _secondaryAttack?.OnEquip(_owner);
    }


    /// <summary>
    /// For CuBots, which only have a primary attack slot.
    /// </summary>
    public void SetPrimarySlot(Sc_BaseAbility primary)
    {
        _primaryAttack = primary;
        _primaryAttack?.OnEquip(_owner);
    }


    /// <summary>
    /// Replaces the R slot with a newly selected branch ability.
    /// Called by Mb_RewardsManager when the player picks their Ultimate Branch.
    /// </summary>
    public void SetRSlot(Sc_BaseAbility r)
    {
        Debug.Log($"[DEBUG] SetRSlot called with: {r?.GetType().Name ?? "null"}");

        // Unequip the old R ability first if one was already set
        _rAbility?.OnUnequip(_owner);

        _rAbility = r;
        _rAbility?.OnEquip(_owner);
    }


    /// <summary>
    /// Returns the ability in the requested slot by name.
    /// Used by Mb_RewardsManager to check upgrade eligibility.
    /// Valid slot names: "Passive", "Q", "E", "R", "Primary", "Secondary"
    /// Returns null if the slot name is unrecognized or the slot is empty.
    /// </summary>
    public Sc_BaseAbility GetAbilityBySlot(AbilitySlot abilitySlot)
    {
        return abilitySlot switch
        {
            AbilitySlot.Passive => _passiveAbility,
            AbilitySlot.Q => _qAbility,
            AbilitySlot.E => _eAbility,
            AbilitySlot.R => _rAbility,
            AbilitySlot.Primary => _primaryAttack,
            AbilitySlot.Secondary => _secondaryAttack,
            _ => null
        };
    }


    /// <summary>
    /// Levels up the ability in the given slot by one level.
    /// Called by Mb_RewardsManager when the player selects an ability upgrade reward.
    /// </summary>
    public void LevelUpAbility(AbilitySlot abilitySlot)
    {
        Sc_BaseAbility ability = GetAbilityBySlot(abilitySlot);

        if (ability == null)
        {
            Debug.LogWarning($"[Mb_AbilityController] LevelUpAbility: slot '{abilitySlot}' is empty.");
            return;
        }

        ability.LevelUp();
    }


    #region Activation Methods          //----------------------------------------
    // These are called directly from Mb_PlayerController's input bindings

    public void ActivatePassive() => TryActivate(_passiveAbility, "Passive");
    public void ActivateQ() => TryActivate(_qAbility, "Q");
    public void ActivateE() => TryActivate(_eAbility, "E");
    public void ActivateR()
    {
        Debug.Log($"[DEBUG] ActivateR called. _rAbility is null: {_rAbility == null}. IsBlocked: {IsBlocked}");
        TryActivate(_rAbility, "R");
    }
    public void ActivatePrimary() => TryActivate(_primaryAttack, "Primary");
    public void ActivateSecondary() => TryActivate(_secondaryAttack, "Secondary");

    // CuBots call this from their AI logic
    public void ActivatePrimaryAsAI() => TryActivate(_primaryAttack, "Primary");


    private void TryActivate(Sc_BaseAbility ability, string slotName)
    {
        // Single choke point — paused or dead means nothing fires
        if (IsBlocked) return;
        if (ability == null) return;


        // DEBUG
        Debug.Log($"[DEBUG] Activating {slotName} ability: {ability.GetType().Name} for {_owner.name}");

        ability.Activate(_owner);

        // Notify listeners that an ability was used.
        // Primal Resonance uses this to add a stack.
        if (ability == _qAbility || ability == _eAbility || ability == _rAbility)   
        {
            OnAbilityActivated?.Invoke(slotName);
        }
    }

    #endregion                      //----------------------------------------


    #region Cleanup                 //----------------------------------------

    /// <summary>
    /// Unequips all abilities. Call on death or scene teardown.
    /// </summary>
    public void UnequipAll()
    {
        _passiveAbility?.OnUnequip(_owner);
        _qAbility?.OnUnequip(_owner);
        _eAbility?.OnUnequip(_owner);
        _rAbility?.OnUnequip(_owner);
        _primaryAttack?.OnUnequip(_owner);
        _secondaryAttack?.OnUnequip(_owner);
    }

    #endregion                      //----------------------------------------


    #region Pause Handling          //----------------------------------------

    private void HandlePause() => _isPaused = true;
    private void HandleResume() => _isPaused = false;

    #endregion                      //----------------------------------------
}


public enum AbilitySlot { Passive, Q, E, R, Primary, Secondary }