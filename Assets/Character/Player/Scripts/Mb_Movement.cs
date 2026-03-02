using UnityEngine;

public class Mb_Movement : MonoBehaviour
{
    [SerializeField] Mb_PlayerController PlayerController;
    private Mb_GuardianAnimator _GuardianAnimator;  // Animation controller

    protected CharacterController _CharacterController;
    protected Camera _MainCamera;

    private float _verticalVelocity = 0f;
    private float _gravity = -20f;
    private float _mouseSensitivity = 0.1f;

    private Vector3 _dashVelocity = Vector3.zero;
    private bool _isDashing = false;

    

    [Header("Cinemachine")]
    public Transform CinemachineCameraTarget;
    public float TopClamp = 30.0f;
    public float BottomClamp = -30.0f;
    public float CameraAngleOverride = 0.0f;
    public bool LockCameraPosition = false;

    private const float _threshold = 0.01f;
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    private void Awake( )
    {
        _CharacterController = GetComponent<CharacterController>( );
        _MainCamera = Camera.main;
        _GuardianAnimator = GetComponent<Mb_GuardianAnimator>( ); 
        if (CinemachineCameraTarget != null)
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
    }

    private void LateUpdate( )
    {
        CameraRotation( );
    }

    private void Update( )
    {
        HandleMovement( );
    }

    private void OnEnable( )
    {
        Mb_PlayerController.OnJumpPressed += Jump;
    }

    private void OnDisable( )
    {
        Mb_PlayerController.OnJumpPressed -= Jump;
    }

    private void Jump( )
    {
        if (_CharacterController.isGrounded)
        {
            float jumpPower = PlayerController.JumpPower.Value( );
            _verticalVelocity = jumpPower;
        }
    }

    private void HandleMovement( )
    {
        // While dashing, bypass all normal movement logic entirely
        if (_isDashing)
        {
            _CharacterController.Move(_dashVelocity * Time.deltaTime);
            return;
        }

        Vector2 moveInput = PlayerController.GetMoveVector( );
        float moveSpeed = PlayerController.MoveSpeed.Value( );
        Vector3 moveDir = transform.TransformDirection(
            new Vector3(moveInput.x, 0f, moveInput.y).normalized
        );

        if (_CharacterController.isGrounded && _verticalVelocity < 0)
            _verticalVelocity = -2f;

        _verticalVelocity += _gravity * Time.deltaTime;

        Vector3 velocity = moveDir * moveSpeed;
        velocity.y = _verticalVelocity;
        _CharacterController.Move(velocity * Time.deltaTime);

        // Speed: normalize the XZ move magnitude so 0 = idle, 1 = running
        moveInput = PlayerController.GetMoveVector( );
        _GuardianAnimator?.SetSpeed(moveInput.magnitude);
        _GuardianAnimator?.SetGrounded(_CharacterController.isGrounded);
        _GuardianAnimator?.SetVerticalVelocity(_verticalVelocity);
    }

    private void CameraRotation( )
    {
        Vector2 lookInput = PlayerController.GetLookVector( ) * _mouseSensitivity;
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

    private static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) angle += 360f;
        if (angle > 360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }

    public void StartDash(Vector3 dashVelocity, float duration)
    {
        _dashVelocity = dashVelocity;
        _isDashing = true;
        StartCoroutine(DashRoutine(duration));
    }

    private System.Collections.IEnumerator DashRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        _isDashing = false;
        _dashVelocity = Vector3.zero;
        // Bleed off vertical momentum so the player doesn't float after a dash
        _verticalVelocity = 0f;
    }
}