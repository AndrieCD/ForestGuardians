using UnityEngine;

public class Mb_Movement : MonoBehaviour
{
    [SerializeField] Mb_PlayerController PlayerController;
    // Components
    protected CharacterController _CharacterController;
    protected Camera _MainCamera;

    // Jump and gravity
    private float _verticalVelocity = 0f;
    private float _gravity = -20f;

    private float _mouseSensitivity = 0.1f;




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
        if (CinemachineCameraTarget != null)
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
    }

    private void LateUpdate( )
    {
        CameraRotation( );
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovement( );
        //HandleRotation( );
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
            // Use JumpPower stat if available, otherwise use a default value
            float jumpPower = PlayerController.JumpPower.Value();
            _verticalVelocity = jumpPower;
            Debug.Log("Jump!");
        }
    }

    private void HandleMovement( )
    {
        // Get input
        Vector2 moveInput = PlayerController.GetMoveVector( );
        float moveX = moveInput.x;
        float moveZ = moveInput.y;

        // Get stat values
        float moveSpeed = PlayerController.MoveSpeed.Value( );

        Vector3 moveDir = transform.TransformDirection(new Vector3(moveX, 0f, moveZ).normalized);

        // Gravity
        if (_CharacterController.isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f; // Small negative to keep grounded
        }
        _verticalVelocity += _gravity * Time.deltaTime;

        Vector3 velocity = moveDir * moveSpeed;
        velocity.y = _verticalVelocity;

        _CharacterController.Move(velocity * Time.deltaTime);
    }

    private void HandleRotation( )
    {
        // Get look input
        Vector2 lookInput = PlayerController.GetLookVector() * _mouseSensitivity;

        // Rotate model on Y axis based on mouse X movement
        transform.Rotate(0f, lookInput.x, 0f);
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
}
