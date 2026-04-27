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

    [Header("Input")]
    [SerializeField] InputActionAsset InputActions;

    private InputActionMap _playerActionMap;
    private InputAction _moveAction, _lookAction;
    private InputAction _qAction, _eAction, _rAction;
    private InputAction _primaryAtkAction, _secondaryAtkAction, _jumpAction;


    protected override void Awake()
    {
        base.Awake();
    }


    protected override void AssignAbilities()
    {
        Abilities.SetSlots(
            passive: new Passive_Ability(_GuardianTemplate.PassiveAbility, this),
            q: new Rajah_Q_Ability(_GuardianTemplate.AbilityQ, this),
            e: new Rajah_E_Ability(_GuardianTemplate.AbilityE, this),
            r: null,    // Assigned at wave 5 when player picks a branch
            primary: new Rajah_Primary(_GuardianTemplate.PrimaryAttack, this),
            secondary: new Rajah_Secondary(_GuardianTemplate.SecondaryAttack, this)
        );
    }


    /// <summary>
    /// Defines Rajah Bagwis's two ultimate branch options.
    /// Each option captures its own SO_Ability reference in the delegate so the
    /// correct data is always passed to the ability constructor — no string matching,
    /// no central factory.
    ///
    /// When a second guardian is added, they override this method in their own
    /// controller class with their own branch types. Nothing here changes.
    /// </summary>
    protected override (Sc_BranchOption branch1, Sc_BranchOption branch2) DefineBranches()
    {
        // Cache the SO references locally so the lambda captures them by value,
        // not by reference to the template — safe even if the template were reassigned
        SO_Ability branch1AbilityData = _GuardianTemplate.AbilityR_Branch1;
        SO_Ability branch2AbilityData = _GuardianTemplate.AbilityR_Branch2;

        Sc_BranchOption branch1 = new Sc_BranchOption
        {
            DisplayData = _GuardianTemplate.BranchDisplay1,
            AbilityData = branch1AbilityData,

            // Lambda captures branch1AbilityData — when the player picks this branch,
            // the rewards manager calls CreateAbility(player) and gets a ready instance
            CreateAbility = owner => new Rajah_R_Branch1(branch1AbilityData, owner)
        };

        Sc_BranchOption branch2 = new Sc_BranchOption
        {
            DisplayData = _GuardianTemplate.BranchDisplay2,
            AbilityData = branch2AbilityData,
            CreateAbility = owner => new Rajah_R_Branch2(branch2AbilityData, owner)
        };

        return (branch1, branch2);
    }


    private void OnEnable()
    {
        _playerActionMap = InputActions.FindActionMap("Player");
        _moveAction = _playerActionMap.FindAction("Move");
        _lookAction = _playerActionMap.FindAction("Look");
        _qAction = _playerActionMap.FindAction("Q");
        _eAction = _playerActionMap.FindAction("E");
        _rAction = _playerActionMap.FindAction("R");
        _primaryAtkAction = _playerActionMap.FindAction("PrimaryAttack");
        _secondaryAtkAction = _playerActionMap.FindAction("SecondaryAttack");
        _jumpAction = _playerActionMap.FindAction("Jump");
        _playerActionMap.Enable();

        _jumpAction.performed += ctx => OnJumpPressed?.Invoke();
        _qAction.performed += ctx => Abilities.ActivateQ();
        _eAction.performed += ctx => Abilities.ActivateE();
        _rAction.performed += ctx => Abilities.ActivateR();
        _primaryAtkAction.performed += ctx => Abilities.ActivatePrimary();
        _secondaryAtkAction.performed += ctx => Abilities.ActivateSecondary();
    }


    private void OnDisable()
    {
        _jumpAction.performed -= ctx => OnJumpPressed?.Invoke();
        _playerActionMap.Disable();
    }


    private void Update()
    {
        if (Health.IsDead) return;
        UpdateDebugInspector();
    }


    private void OnDestroy()
    {
        Abilities.UnequipAll();
    }


    #region Input Getters
    public Vector2 GetMoveVector() => _moveAction.ReadValue<Vector2>();
    public Vector2 GetLookVector() => _lookAction.ReadValue<Vector2>();
    #endregion


    #region Debug
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
    #endregion
}