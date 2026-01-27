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

    private void Awake( )
    {
        _CharacterController = GetComponent<CharacterController>( );
        _MainCamera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovement( );
        HandleRotation( );
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

        // Rotate camera on X axis based on mouse Y movement
        if (_MainCamera != null)
        {
            // Clamp vertical rotation between -90 and 90 degrees
            float currentXRotation = _MainCamera.transform.eulerAngles.x;   // Get current X rotation
            float newXRotation = currentXRotation - lookInput.y;            // get new X rotation based on input
            newXRotation = ( newXRotation > 180 ) ? newXRotation - 360 : newXRotation; // Convert to -180 to 180 range
            newXRotation = Mathf.Clamp(newXRotation, -90f, 90f);            // Clamp to -90 to 90
            _MainCamera.transform.eulerAngles = new Vector3(newXRotation, _MainCamera.transform.eulerAngles.y, 0f);

            // Move the camera closer along the z-axis when looking up or down
            // TODO
        }
    }
}
