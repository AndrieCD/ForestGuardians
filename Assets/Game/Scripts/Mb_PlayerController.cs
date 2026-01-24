using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Mb_PlayerController : Mb_GuardianBase
{
    // Event for Hybrid Passives to listen to
    //public event System.Action<float, CharacterBase> OnAttackHit;

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


    // Components
    private CharacterController _characterController;
    private Camera _mainCamera;

    // Input Action Maps and Actions
    [SerializeField] InputActionAsset InputAction;
    private InputActionMap _playerActionMap;
    private InputAction _moveAction, _lookAction, _qAction, _eAction, _rAction, _primaryAtkAction, _secondaryAtkAction;

    // Cooldown trackers
    private float _cooldownTimerQ;
    private float _cooldownTimerE;

    #region Initializations and setup
    protected override void Awake( )
    {

        base.Awake( ); // Sets up Stats
        _characterController = GetComponent<CharacterController>( );
        _mainCamera = Camera.main;

        // Equip Abilities (Starts Passives)
        _PassiveAbility?.OnEquip(this);
        _QAbility?.OnEquip(this);
        _EAbility?.OnEquip(this);
        _RAbility?.OnEquip(this);

        // TEST // TEST // TEST // 
        // TEST // TEST // TEST // 
        InstantiateVisualModel( );
    }

    private void InstantiateVisualModel( )
    {
        if (_GuardianTemplate.ModelPrefab != null)
        {
            // Spawn the character model inside the player prefab container
            GameObject model = Instantiate(_GuardianTemplate.ModelPrefab, transform);

            // Reparent main camera to the model's camera mount point if it exists
            Transform cameraMount = gameObject.transform.Find("CameraMount");
            if (cameraMount != null && _mainCamera != null)
            {
                _mainCamera.transform.SetParent(cameraMount, false);
                _mainCamera.transform.localPosition = Vector3.zero;
                _mainCamera.transform.localRotation = Quaternion.identity;
            }

        }
    }

    private void OnEnable( )
    {
        // Setup Input Actions
        _playerActionMap = InputAction.FindActionMap("Player");
        _moveAction = _playerActionMap.FindAction("Move");
        _lookAction = _playerActionMap.FindAction("Look");
        _qAction = _playerActionMap.FindAction("Q");
        _eAction = _playerActionMap.FindAction("E");
        _rAction = _playerActionMap.FindAction("R");
        _primaryAtkAction = _playerActionMap.FindAction("Attack");
        _playerActionMap.Enable( );
    }
    private void OnDisable( )
    {
        _playerActionMap.Disable( );
    }

    private void Start( )
    {
        // Initialize cooldowns
        _cooldownTimerQ = 0f;
        _cooldownTimerE = 0f;

        // Bind ability events 
        _qAction.performed += ctx =>
        {
            if (_cooldownTimerQ <= 0 && _QAbility != null)
            {
                _QAbility.Activate(this);
                //_cooldownTimerQ = AbilityQ.cooldownTime;
            }
        };

        _eAction.performed += ctx =>
        {
            if (_cooldownTimerE <= 0 && _EAbility != null)
            {
                _EAbility.Activate(this);
                //_cooldownTimerE = AbilityE.cooldownTime;
            }
        };

        _rAction.performed += ctx =>
        {
            if (_RAbility != null)
            {
                _RAbility.Activate(this);
            }
        };

        _primaryAtkAction.performed += ctx =>
        {
            Debug.Log("Primary Attack triggered");


        };
    }
    #endregion

    #region Update Loop and methods
    private void Update( )
    {
        if (IsDead) return;

        HandleMovement( );
        HandleRotation( );
        UpdateCooldowns( );

        UpdateDebugInspector();
    }

    private void UpdateDebugInspector( )
    {
        GuardianName = _GuardianName;
        CurrentHealthValue = _CurrentHealth;
        MaxHPValue = MaxHealth.Value();
        HealthRegenValue = HealthRegen.Value();
        MoveSpeedValue = MoveSpeed.Value();
        AttackSpeedValue = AttackSpeed.Value();
        AttackPowerValue = AttackPower.Value();
        AbilityPowerValue = AbilityPower.Value();
        CooldownReductionValue = CooldownReduction.Value();
        CriticalChanceValue = CriticalChance.Value();
        CriticalDamageValue = CriticalDamage.Value();
        LifestealValue = Lifesteal.Value( );
        CurrentShieldValue = CurrentShield.Value( );
    }

    private void HandleMovement( )
    {
        // Get input
        Vector2 moveInput = _moveAction.ReadValue<Vector2>( );
        float moveX = moveInput.x;
        float moveZ = moveInput.y;

        Vector3 moveDir = transform.TransformDirection(new Vector3(moveX, 0f, moveZ).normalized);

        if (moveDir.magnitude >= 0.1f)
        {
            // Move via CharacterController
            _characterController.Move(moveDir * MoveSpeed.Value() * Time.deltaTime);
        }

        // Apply Gravity (Simple)
        //_characterController.Move(Physics.gravity * Time.deltaTime);
    }

    private void HandleRotation( )
    {
        // Get look input
        Vector2 lookInput = _lookAction.ReadValue<Vector2>( );

        // Rotate model on Y axis based on mouse X movement
        transform.Rotate(0f, lookInput.x, 0f);

        // Rotate camera on X axis based on mouse Y movement
        if (_mainCamera != null)
        {
            // TODO
        }

    }

    private void UpdateCooldowns( )
    {
        if (_cooldownTimerQ > 0) _cooldownTimerQ -= Time.deltaTime;
        if (_cooldownTimerE > 0) _cooldownTimerE -= Time.deltaTime;
    }
#endregion

    // Example of triggering the event
    public void DealDamage(I_Damageable target)
    {
        float damage = AttackPower.Value(); // Uses the calculated stat
        target.TakeDamage(damage);

        // Notify listeners (like Lifesteal passives)
        //OnAttackHit?.Invoke(damage, this);

        //if (target.IsDead) NotifyEnemyKilled( );
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
