// Mb_PlayerController.cs
// The concrete Guardian class for Rajah Bagwis.
// Handles input via Unity's Input System and routes it to
// AbilityController and Movement.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Mb_PlayerController : Mb_GuardianBase
{
    public static event Action OnJumpPressed;

    private ActionDisableFlags _disableFlags = ActionDisableFlags.None;

    [Header("Debug — Runtime Stat Viewer")]
    public string GuardianName = "";
    public float CurrentHealthValue = 0;
    public float MaxHPValue = 0;
    public float HealthRegenValue = 0;
    public float MoveSpeedValue = 0;
    public float AttackSpeedValue = 0;
    public float AttackPowerValue = 0;
    public float AbilityPowerValue = 0;
    public float CooldownReductionValue = 0;
    public float CriticalChanceValue = 0;
    public float CriticalDamageValue = 0;
    public float LifestealValue = 0;
    public float CurrentShieldValue = 0;

    private InputActionMap _playerActionMap;
    private InputAction _moveAction, _lookAction;
    private InputAction _qAction, _eAction, _rAction;
    private InputAction _primaryAtkAction, _secondaryAtkAction, _jumpAction;


    protected override void Awake()
    {
        Sc_BuildLogger.Trace("PlayerController Awake START");

        base.Awake();

        Sc_BuildLogger.Trace("PlayerController Awake END");
    }


    protected override void OnEnable()
    {
        base.OnEnable();

        Sc_BuildLogger.Trace("PlayerController OnEnable START");


        _playerActionMap = InputSystem.actions.FindActionMap("Player");

        if (_playerActionMap == null)
        {
            Debug.LogError("[Mb_PlayerController] Could not find Action Map 'Player'.");
            return;
        }

        _moveAction = _playerActionMap.FindAction("Move");
        _lookAction = _playerActionMap.FindAction("Look");
        _qAction = _playerActionMap.FindAction("Q");
        _eAction = _playerActionMap.FindAction("E");
        _rAction = _playerActionMap.FindAction("R");
        _primaryAtkAction = _playerActionMap.FindAction("PrimaryAttack");
        _secondaryAtkAction = _playerActionMap.FindAction("SecondaryAttack");
        _jumpAction = _playerActionMap.FindAction("Jump");

        _jumpAction.performed += HandleJump;
        _qAction.performed += HandleQ;
        _eAction.performed += HandleE;
        _rAction.performed += HandleR;
        _primaryAtkAction.performed += HandlePrimary;
        _secondaryAtkAction.performed += HandleSecondary;

        _playerActionMap.Enable();

        Sc_BuildLogger.Trace("PlayerController OnEnable START");

    }

    protected override void AssignAbilities()
    {
        // Guard every SO reference — a missing assignment in the Inspector
        // causes a NullReferenceException in the ability constructor in builds,
        // even if the Editor appears to handle it gracefully.
        if (_GuardianTemplate.PassiveAbility == null)
            Debug.LogError("[Mb_PlayerController] PassiveAbility SO is not assigned on the Guardian template.");
        if (_GuardianTemplate.AbilityQ == null)
            Debug.LogError("[Mb_PlayerController] AbilityQ SO is not assigned on the Guardian template.");
        if (_GuardianTemplate.AbilityE == null)
            Debug.LogError("[Mb_PlayerController] AbilityE SO is not assigned on the Guardian template.");
        if (_GuardianTemplate.PrimaryAttack == null)
            Debug.LogError("[Mb_PlayerController] PrimaryAttack SO is not assigned on the Guardian template.");
        if (_GuardianTemplate.SecondaryAttack == null)
            Debug.LogError("[Mb_PlayerController] SecondaryAttack SO is not assigned on the Guardian template.");


        if (_GuardianTemplate.GuardianID == GuardiansEnum.RajahBagwis)
        {
            Abilities.SetSlots(
                passive: _GuardianTemplate.PassiveAbility != null ? new Passive_Ability(_GuardianTemplate.PassiveAbility, this) : null,
                q: _GuardianTemplate.AbilityQ != null ? new Rajah_Q_Ability(_GuardianTemplate.AbilityQ, this) : null,
                e: _GuardianTemplate.AbilityE != null ? new Rajah_E_Ability(_GuardianTemplate.AbilityE, this) : null,
                r: null,
                primary: _GuardianTemplate.PrimaryAttack != null ? new Rajah_Primary(_GuardianTemplate.PrimaryAttack, this) : null,
                secondary: _GuardianTemplate.SecondaryAttack != null ? new Rajah_Secondary(_GuardianTemplate.SecondaryAttack, this) : null
            );
        }
        else if (_GuardianTemplate.GuardianID == GuardiansEnum.Mari)
        {
            Abilities.SetSlots(
                passive: _GuardianTemplate.PassiveAbility != null ? new Mari_Passive(_GuardianTemplate.PassiveAbility, this) : null,
                q: _GuardianTemplate.AbilityQ != null ? new Mari_Q(_GuardianTemplate.AbilityQ, this) : null,
                e: _GuardianTemplate.AbilityE != null ? new Mari_E(_GuardianTemplate.AbilityE, this) : null,
                r: null,
                primary: _GuardianTemplate.PrimaryAttack != null ? new Mari_Primary(_GuardianTemplate.PrimaryAttack, this) : null,
                secondary: _GuardianTemplate.SecondaryAttack != null ? new Mari_Secondary(_GuardianTemplate.SecondaryAttack, this) : null
            );
        }
        else
        {
            Debug.LogError($"[Mb_PlayerController] Unrecognized GuardianID '{_GuardianTemplate.GuardianID}' in template.");
        }
    }


    protected override (Sc_BranchOption branch1, Sc_BranchOption branch2) DefineBranches()
    {
        SO_Ability branch1AbilityData = _GuardianTemplate.AbilityR_Branch1;
        SO_Ability branch2AbilityData = _GuardianTemplate.AbilityR_Branch2;

        Sc_BranchOption branch1 = new Sc_BranchOption
        {
            DisplayData = _GuardianTemplate.BranchDisplay1,
            AbilityData = branch1AbilityData,
            CreateAbility = owner => CreateBranchAbility(1, branch1AbilityData, owner)
        };

        Sc_BranchOption branch2 = new Sc_BranchOption
        {
            DisplayData = _GuardianTemplate.BranchDisplay2,
            AbilityData = branch2AbilityData,
            CreateAbility = owner => CreateBranchAbility(2, branch2AbilityData, owner)
        };

        return (branch1, branch2);
    }

    private Sc_BaseAbility CreateBranchAbility(int branchNumber, SO_Ability abilityData, Mb_CharacterBase owner)
    {
        return _GuardianTemplate.GuardianID switch
        {
            GuardiansEnum.RajahBagwis => branchNumber == 1
                ? new Rajah_R_Branch1(abilityData, owner)
                : new Rajah_R_Branch2(abilityData, owner),

            GuardiansEnum.Mari => branchNumber == 1
                ? new Mari_R_Branch1(abilityData, owner)
                : new Mari_R_Branch2(abilityData, owner),

            _ => null
        };
    }



    protected override void OnDisable()
    {
        base.OnDisable();

        // Named method references match exactly — these actually unsubscribe correctly.
        if (_jumpAction != null) _jumpAction.performed -= HandleJump;
        if (_qAction != null) _qAction.performed -= HandleQ;
        if (_eAction != null) _eAction.performed -= HandleE;
        if (_rAction != null) _rAction.performed -= HandleR;
        if (_primaryAtkAction != null) _primaryAtkAction.performed -= HandlePrimary;
        if (_secondaryAtkAction != null) _secondaryAtkAction.performed -= HandleSecondary;

        _playerActionMap?.Disable();
    }


    private void OnDestroy()
    {
        Abilities.UnequipAll();
    }


    private void Update()
    {
        if (Health.IsDead) return;
        UpdateDebugInspector();
    }


    #region Input Handlers          //----------------------------------------
    // Named methods so OnDisable can unsubscribe them correctly.
    // Never use lambdas for input subscriptions that need to be removed.

    private void HandleJump(InputAction.CallbackContext ctx)
        => OnJumpPressed?.Invoke();

    private void HandleQ(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing)
            return;

        if (!IsDisabled(ActionDisableFlags.AbilityQ))
            Abilities.ActivateQ();
    }

    private void HandleE(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing)
            return;

        if (!IsDisabled(ActionDisableFlags.AbilityE))
            Abilities.ActivateE();
    }

    private void HandleR(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing)
            return;

        Debug.Log("[DEBUG] R key pressed — input received.");
        if (!IsDisabled(ActionDisableFlags.AbilityR))
            Abilities.ActivateR();
    }

    private void HandlePrimary(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing)
            return;

        if (!IsDisabled(ActionDisableFlags.PrimaryAttack))
            Abilities.ActivatePrimary();
    }

    private void HandleSecondary(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing)
            return;

        if (!IsDisabled(ActionDisableFlags.SecondaryAttack))
            Abilities.ActivateSecondary();
    }

    #endregion                      //----------------------------------------


    #region Input Getters           //----------------------------------------

    public Vector2 GetMoveVector()
    {
        if (GameManager.Instance.CurrentState != GameState.Playing)
            return Vector2.zero;

        return _moveAction != null
        ? _moveAction.ReadValue<Vector2>()
        : Vector2.zero;
    }
    public Vector2 GetLookVector()
    {
        if (GameManager.Instance.CurrentState != GameState.Playing)
            return Vector2.zero;

        return _lookAction != null
        ? _lookAction.ReadValue<Vector2>()
        : Vector2.zero;
    }

    #endregion                      //----------------------------------------


    #region Disable Flags           //----------------------------------------

    public void AddDisable(ActionDisableFlags flags) => _disableFlags |= flags;
    public void RemoveDisable(ActionDisableFlags flags) => _disableFlags &= ~flags;
    public bool IsDisabled(ActionDisableFlags flag) => (_disableFlags & flag) != 0;

    #endregion                      //----------------------------------------


    #region Debug                   //----------------------------------------

    private void UpdateDebugInspector()
    {
        GuardianName = _CharacterName;
        CurrentHealthValue = Health.CurrentHealth;
        MaxHPValue = Stats.MaxHealth.GetValue();
        HealthRegenValue = Stats.HealthRegen.GetValue();
        MoveSpeedValue = Stats.MoveSpeed.GetValue();
        AttackSpeedValue = Stats.AttackSpeed.GetValue();
        AttackPowerValue = Stats.AttackPower.GetValue();
        AbilityPowerValue = Stats.AbilityPower.GetValue();
        CooldownReductionValue = Stats.Haste.GetValue();
        CriticalChanceValue = Stats.CriticalChance.GetValue();
        CriticalDamageValue = Stats.CriticalDamage.GetValue();
        LifestealValue = Stats.Lifesteal.GetValue();
    }

    #endregion                      //----------------------------------------
}
