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

    /// <summary>
    /// Retrieves an ability by slot name. Used by UI and RewardsManager
    /// without exposing the private fields directly.
    /// </summary>
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
    /// Assigns the R (ultimate) slot after initial setup — used by the Ultimate Branch
    /// selection system. Equips the new ability immediately.
    /// Only Mb_RewardsManager should call this, and only once per stage.
    /// </summary>
    public void SetRSlot(Sc_BaseAbility branch)
    {
        // If somehow an R ability was already set, unequip it cleanly first
        _rAbility?.OnUnequip(_owner);

        _rAbility = branch;
        _rAbility?.OnEquip(_owner);

        Debug.Log($"[Mb_AbilityController] R slot set to: {branch?.GetType().Name}");
    }


    /// <summary>
    /// Increments the level of the ability in the named slot.
    /// Called by Mb_RewardsManager when the player picks an ability upgrade reward.
    /// Does nothing if the slot is empty or already at max level.
    /// </summary>
    public void LevelUpAbility(string slot)
    {
        Sc_BaseAbility ability = GetAbilityBySlot(slot);

        if (ability == null)
        {
            Debug.LogWarning($"[Mb_AbilityController] LevelUpAbility: slot '{slot}' is empty.");
            return;
        }

        // LevelUp() on the base class handles the max level guard and fires OnLevelUp()
        ability.LevelUp();
    }


    #region Activation Methods          //----------------------------------------

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