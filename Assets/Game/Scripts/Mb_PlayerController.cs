using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Class for managing player controls, receiving inputs then acting on the Guardian character based on those inputs.
/// </summary>
public class Mb_PlayerController : Mb_GuardianBase
{
    public static event Action OnJumpPressed;

    [Header("Guardian Stats")] // For Inspector visibility
    // Display runtime stats in Inspector for debugging
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
    //[SerializeField] List<String> ActiveModifiers = new List<String>( );

    // Input Action Maps and Actions
    [SerializeField] InputActionAsset InputAction;
    private InputActionMap _playerActionMap;
    private InputAction _qAction, _eAction, _rAction, _primaryAtkAction, _secondaryAtkAction, _jumpAction;  // BUTTON ACTIONS
    protected InputAction _MoveAction, _LookAction;
    #region InputAction Getters
    public Vector2 GetMoveVector( )
    {
        return _MoveAction.ReadValue<Vector2>( );
    }
    public Vector2 GetLookVector( )
    {
        return _LookAction.ReadValue<Vector2>( );
    }
    #endregion


    protected override void Awake( )
    {

        base.Awake( ); // Sets up Stats
      
        //// Equip Abilities (Starts Passives)
        //_PassiveAbility?.OnEquip(this);
        //_QAbility?.OnEquip(this);
        //_EAbility?.OnEquip(this);
        //_RAbility?.OnEquip(this);
    }

    //private void InstantiateVisualModel( )
    //{
    //    if (_GuardianTemplate.ModelPrefab != null)
    //    {
    //        // Spawn the character model inside the player prefab container
    //        GameObject model = Instantiate(_GuardianTemplate.ModelPrefab, transform);

    //        // Reparent main camera to the model's camera mount point if it exists
    //        Transform cameraMount = gameObject.transform.Find("CameraMount");
    //        if (cameraMount != null && _mainCamera != null)
    //        {
    //            _mainCamera.transform.SetParent(cameraMount, false);
    //            _mainCamera.transform.localPosition = Vector3.zero;
    //            _mainCamera.transform.localRotation = Quaternion.identity;
    //        }

    //    }
    //}

    private void OnEnable( )
    {
        // Setup Input Actions
        _playerActionMap = InputAction.FindActionMap("Player");
        _MoveAction = _playerActionMap.FindAction("Move");
        _LookAction = _playerActionMap.FindAction("Look");
        _qAction = _playerActionMap.FindAction("Q");
        _eAction = _playerActionMap.FindAction("E");
        _rAction = _playerActionMap.FindAction("R");
        _primaryAtkAction = _playerActionMap.FindAction("PrimaryAttack");
        _secondaryAtkAction = _playerActionMap.FindAction("SecondaryAttack");
        _jumpAction = _playerActionMap.FindAction("Jump");
        _playerActionMap.Enable( );

        // Bind Button Actions
        _jumpAction.performed += ctx => OnJumpPressed?.Invoke( );
        _qAction.performed += ctx => _QAbility.Activate(this);
        _eAction.performed += ctx => _EAbility.Activate(this);
        //_rAction.performed += ctx => _RAbility.Activate(this); // Two versions, needs deeper design
        _primaryAtkAction.performed += ctx => _PrimaryAttack.Activate(this);
        _secondaryAtkAction.performed += ctx => _SecondaryAttack.Activate(this);
    }

    private void OnDisable( )
    {
        _jumpAction.performed -= ctx => OnJumpPressed?.Invoke( );
        _playerActionMap.Disable( );
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        OnJumpPressed?.Invoke( );
    }

    private void Update( )
    {
        if (IsDead) return;

        UpdateDebugInspector( );
    }

    private void UpdateDebugInspector( )
    {
        GuardianName = _GuardianName;
        CurrentHealthValue = _CurrentHealth;
        MaxHPValue = MaxHealth.Value( );
        HealthRegenValue = HealthRegen.Value( );
        MoveSpeedValue = MoveSpeed.Value( );
        AttackSpeedValue = AttackSpeed.Value( );
        AttackPowerValue = AttackPower.Value( );
        AbilityPowerValue = AbilityPower.Value( );
        CooldownReductionValue = CooldownReduction.Value( );
        CriticalChanceValue = CriticalChance.Value( );
        CriticalDamageValue = CriticalDamage.Value( );
        LifestealValue = Lifesteal.Value( );
        CurrentShieldValue = Shielding.Value( );
        //// Active Modifiers
        //foreach (var modifier in _ActiveModifiers)
        //{
        //    if (!ActiveModifiers.Contains(modifier.ModifierName))
        //        ActiveModifiers.Add(modifier.ModifierName);
        //}
    }

    private void OnDestroy( )
    {
        // Clean up stats/modifiers when scene ends
        _PassiveAbility?.OnUnequip(this);
        //AbilityQ?.OnUnequip(this);
        //AbilityE?.OnUnequip(this);
        //AbilityR?.OnUnequip(this);
    }
}
