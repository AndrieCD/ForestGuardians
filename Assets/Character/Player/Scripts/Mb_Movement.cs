using System;
using UnityEngine;

/// <summary>
/// Handles all physical movement for the player: walking, gravity, jumping, dashing, and camera rotation.
/// 
/// Reads MoveSpeed and JumpPower directly from Mb_StatBlock on the same GameObject,
/// so stat changes from augments apply immediately without any extra wiring.
///
/// Input vectors are pulled from Mb_PlayerController each frame.
/// Movement is blocked while paused by subscribing to Mb_PauseManager events.
/// </summary>
public class Mb_Movement : MonoBehaviour
{
    // PlayerController lives on the same GameObject — used only to get input vectors
    [SerializeField] Mb_PlayerController _playerController;

    private Mb_StatBlock _statBlock;
    private CharacterController _characterController;
    private Camera _mainCamera;

    private float _verticalVelocity = 0f;
    private float _gravity = -20f;
    private float _mouseSensitivity = 0.1f;

    private bool _wasGrounded = true; // Used to detect landing events in Update()

    private Vector3 _dashVelocity = Vector3.zero;
    //private bool _isDashing = false;
    private bool _isPaused = false;
    private MovementState _movementState = MovementState.Normal;


    [Header("Cinemachine")]
    public Transform CinemachineCameraTarget;
    public float TopClamp = 30.0f;
    public float BottomClamp = -30.0f;
    public float CameraAngleOverride = 0.0f;
    public bool LockCameraPosition = false;

    private const float _threshold = 0.01f;
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    #region EVENTS
    public event Action OnDashStarted;
    public event Action OnDashEnded;
    public static event Action OnLanded;           // fires once when isGrounded transitions false → true
    public static event Action OnJumped;           // fires once when the player successfully initiates a jump
    public event Action<bool> OnGroundedChanged; // true = just landed, false = just left ground
    #endregion


    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _mainCamera = Camera.main;

        // Fetch StatBlock from the same GameObject to read MoveSpeed and JumpPower
        _statBlock = GetComponent<Mb_StatBlock>();
        if (_statBlock == null)
            Debug.LogError($"[Mb_Movement] No Mb_StatBlock found on {gameObject.name}.");

        if (CinemachineCameraTarget != null)
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
    }

    private void OnEnable()
    {
        Mb_PlayerController.OnJumpPressed += Jump;
        Mb_PauseManager.OnPaused += HandlePause;
        Mb_PauseManager.OnResumed += HandleResume;
    }

    private void OnDisable()
    {
        Mb_PlayerController.OnJumpPressed -= Jump;
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;
    }

    private void Update()
    {
        if (_isPaused) return;

        // Track a _wasGrounded bool in Update() to detect landing and fire OnLanded / OnGroundedChanged.
        bool isGrounded = _characterController != null && _characterController.isGrounded;

        // Detect landing (false -> true)
        if (isGrounded && !_wasGrounded)
        {
            Mb_Movement.OnLanded?.Invoke();
            OnGroundedChanged?.Invoke(true);
        }
        // Detect leaving ground (true -> false)
        else if (!isGrounded && _wasGrounded)
        {
            OnGroundedChanged?.Invoke(false);
            OnJumped?.Invoke();
        }

        _wasGrounded = isGrounded;

        HandleMovement();
    }

    private void LateUpdate()
    {
        if (_isPaused) return;
        CameraRotation();
    }

    private void Jump()
    {
        if (_isPaused) return;
        if (_playerController.IsDisabled(ActionDisableFlags.Jump))
            return;
        if (!_characterController.isGrounded) return;

        float jumpPower = _statBlock.JumpPower.GetValue();
        _verticalVelocity = jumpPower;

        // For animator
        _playerController.GuardianAnimator.SetGrounded(false);
    }

    private void HandleMovement()
    {
        if (_playerController.IsDisabled(ActionDisableFlags.Movement))
            return;

        // While dashing, bypass all normal movement logic entirely
        if (_movementState == MovementState.Dashing)
        {
            if (_playerController.IsDisabled(ActionDisableFlags.Dash))
                return;
            _characterController.Move(_dashVelocity * Time.deltaTime);
            return;
        }

        Vector2 moveInput = _playerController.GetMoveVector();
        float moveSpeed = _statBlock.MoveSpeed.GetValue(); // Read fresh — catches augment changes mid-wave

        Vector3 moveDir = transform.TransformDirection(
            new Vector3(moveInput.x, 0f, moveInput.y).normalized
        );

        // Keep character grounded instead of slowly floating down slopes
        if (_characterController.isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f;
            // For animator 
            _playerController.GuardianAnimator.SetGrounded(true);

        }

        _verticalVelocity += _gravity * Time.deltaTime;

        Vector3 velocity = moveDir * moveSpeed;
        velocity.y = _verticalVelocity;
        _characterController.Move(velocity * Time.deltaTime);

        // For animator
        _playerController.GuardianAnimator.SetVerticalVelocity(velocity.y);


        // (XZ velocity) Animation
        _playerController.GuardianAnimator.SetSpeed(moveInput.magnitude);
    }

    private void CameraRotation()
    {
        if (_playerController.IsDisabled(ActionDisableFlags.Rotation))
            return;

        Vector2 lookInput = _playerController.GetLookVector() * _mouseSensitivity;

        if (lookInput.sqrMagnitude >= _threshold && !LockCameraPosition)
        {
            _cinemachineTargetYaw += lookInput.x;
            _cinemachineTargetPitch -= lookInput.y;
        }

        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        if (CinemachineCameraTarget != null)
        {
            CinemachineCameraTarget.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0f, 0f);
            transform.rotation = Quaternion.Euler(0f, _cinemachineTargetYaw, 0f);
        }
    }

    public void StartDash(Vector3 dashVelocity, float duration)
    {
        _dashVelocity = dashVelocity;
        _movementState = MovementState.Dashing;
        StartCoroutine(DashRoutine(duration));
    }

    // Add alongside StartDash():
    public void ApplyDisplacement(Vector3 velocity, float duration)  // external, no flag check
    {
        _dashVelocity = velocity;
        _movementState = MovementState.Knockback;
        StartCoroutine(DashRoutine(duration));
    }

    private System.Collections.IEnumerator DashRoutine(float duration)
    {
        OnDashStarted?.Invoke();
        yield return new WaitForSeconds(duration);
        _movementState = MovementState.Normal;
        _dashVelocity = Vector3.zero;
        // Bleed off vertical momentum so the player doesn't float after dashing
        _verticalVelocity = 0f;
        OnDashEnded?.Invoke();
    }

    private static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) angle += 360f;
        if (angle > 360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }

    #region Pause Handling
    private void HandlePause() => _isPaused = true;
    private void HandleResume() => _isPaused = false;
    #endregion
}