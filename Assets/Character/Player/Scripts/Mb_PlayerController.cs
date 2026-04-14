using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The concrete Guardian class for the player.
/// Handles input via Unity's Input System and routes it to
/// AbilityController and Movement — it does not contain any game logic itself.
///
/// Inherits from Mb_GuardianBase which sets up stats, health, and abilities.
/// </summary>
public class Mb_PlayerController : Mb_GuardianBase
{

    // Movement subscribes to this to trigger jumping without a direct reference back here
    public static event Action OnJumpPressed;

    [Header("Debug — Runtime Stat Viewer")]
    // These are Inspector-only display fields for debugging — not used in logic
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


    // Input system references
    private InputActionMap _playerActionMap;
    private InputAction _moveAction, _lookAction;
    private InputAction _qAction, _eAction, _rAction;
    private InputAction _primaryAtkAction, _secondaryAtkAction, _jumpAction;


    protected override void Awake()
    {
        // Base Awake fetches components and calls InitializeFromTemplate()
        // which in turn calls AssignAbilities() defined below
        base.Awake();
    }

    /// <summary>
    /// Assigns Rajah Bagwis's ability set to the AbilityController slots.
    /// Called automatically during InitializeFromTemplate() in Mb_GuardianBase.
    /// </summary>
    protected override void AssignAbilities()
    {
        Abilities.SetSlots(
            passive: new Passive_Ability(_GuardianTemplate.PassiveAbility, this),
            q: new Rajah_Q_Ability(_GuardianTemplate.AbilityQ, this),
            e: new Rajah_E_Ability(_GuardianTemplate.AbilityE, this),
            r: null,    // TODO: R ability pending design decision (two branches)
            primary: new Rajah_Primary(_GuardianTemplate.PrimaryAttack, this),
            secondary: new Rajah_Secondary(_GuardianTemplate.SecondaryAttack, this)
        );
    }

    private void OnEnable()
    {
        // Find and cache all input actions from the asset
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

        // Bind input to AbilityController — pause blocking is handled inside AbilityController,
        // so we don't need any pause checks here
        _jumpAction.performed += ctx => OnJumpPressed?.Invoke();
        _qAction.performed += ctx => Abilities.ActivateQ();
        _eAction.performed += ctx => Abilities.ActivateE();
        _primaryAtkAction.performed += ctx => Abilities.ActivatePrimary();
        _secondaryAtkAction.performed += ctx => Abilities.ActivateSecondary();
        // _rAction.performed         += ctx => Abilities.ActivateR(); // TODO: pending design decision
    }

    private void OnDisable()
    {
        // Always unsubscribe when disabled to prevent ghost listeners
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
        // Clean up all abilities when the scene ends
        Abilities.UnequipAll();
    }

    #region Input Getters
    // Mb_Movement calls these to get the current frame's input vectors
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
        //CurrentShieldValue = Stats.Shielding.Value();
    }
    #endregion
}