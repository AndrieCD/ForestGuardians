using UnityEngine;

/// <summary>
/// Owns all ability slots for a character and controls when they can activate.
/// Sits as a component on the Guardian GameObject.
///
/// Mb_PlayerController calls ActivateQ(), ActivatePrimary(), etc. from input bindings.
/// Mb_GuardianBase calls SetSlots() during InitializeFromTemplate() to wire up abilities.
///
/// This is the single place where pause blocks ability use — nothing else needs to change.
/// </summary>
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

    // Method to retrieve an ability by slot name, for UI display and other cases where we don't want to expose the fields directly
    public Sc_BaseAbility GetAbilityBySlot(string slot)
    {
        return slot switch
        {
            "Passive" => _passiveAbility,
            "Q" => _qAbility,
            "E" => _eAbility,
            "R" => _rAbility,
            "Primary" => _primaryAttack,
            "Secondary" => _secondaryAttack,
            _ => null
        };
    }

    #endregion                  //----------------------------------------


    #region State               //----------------------------------------

    private bool _isPaused = false;

    // If true, all ability activation is blocked (e.g. during cutscenes, stun, death)
    public bool IsBlocked => _isPaused || _owner.Health.IsDead;

    #endregion                  //----------------------------------------


    private void Awake()
    {
        _owner = GetComponent<Mb_CharacterBase>();

        if (_owner == null)
            Debug.LogError($"[Mb_AbilityController] No Mb_CharacterBase found on {gameObject.name}.");
    }


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


    #region Activation Methods          //----------------------------------------
    // These are called directly from Mb_PlayerController's input bindings

    public void ActivatePassive() => TryActivate(_passiveAbility);
    public void ActivateQ() => TryActivate(_qAbility);
    public void ActivateE() => TryActivate(_eAbility);
    public void ActivateR() => TryActivate(_rAbility);
    public void ActivatePrimary() => TryActivate(_primaryAttack);
    public void ActivateSecondary() => TryActivate(_secondaryAttack);


    // CuBots call this from their AI logic
    public void ActivatePrimaryAsAI() => TryActivate(_primaryAttack);


    private void TryActivate(Sc_BaseAbility ability)
    {
        // Single choke point — paused or dead means nothing fires
        if (IsBlocked) return;
        ability?.Activate(_owner);
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