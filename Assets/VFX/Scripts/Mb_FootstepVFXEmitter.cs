using UnityEngine;

public class Mb_FootstepVFXEmitter : MonoBehaviour
{
    [Header("Footstep VFX")]
    [SerializeField] private VFXType _GroundStepVFX = VFXType.Footstep_Generic;
    [SerializeField] private VFXType _WaterStepVFX = VFXType.Footstep_Water;

    [Header("Jump / Land VFX")]
    [SerializeField] private VFXType _JumpVFX = VFXType.Guardian_Jump;
    [SerializeField] private VFXType _LandVFX = VFXType.Guardian_Land;

    [SerializeField] private float _JumpLandHeightOffset = 0.1f;

    [Header("Timing")]
    [SerializeField] private float _BaseStepInterval = 0.4f;

    [Header("Surface Detection")]
    [SerializeField] private float _SurfaceCheckDistance = 1.2f;
    [SerializeField] private string _WaterLayerName = "Water";

    private Mb_CharacterBase _character;
    private CharacterController _controller;
    private Mb_Movement _movement;

    private float _stepTimer;

    private const float MIN_SPEED_THRESHOLD = 0.1f;

    private void Awake()
    {
        _character = GetComponent<Mb_CharacterBase>();
        _controller = GetComponent<CharacterController>();
        _movement = GetComponent<Mb_Movement>();

        if (_character == null)
            Debug.LogError($"[Mb_FootstepVFXEmitter] No Mb_CharacterBase on {gameObject.name}.");

        if (_movement == null)
            Debug.LogError($"[Mb_FootstepVFXEmitter] No Mb_Movement on {gameObject.name}.");
    }

    private void OnEnable()
    {
        if (_movement != null)
        {
            Mb_Movement.OnJumped += HandleJump;
            Mb_Movement.OnLanded += HandleLand;
        }
    }

    private void OnDisable()
    {
        if (_movement != null)
        {
            Mb_Movement.OnJumped -= HandleJump;
            Mb_Movement.OnLanded -= HandleLand;
        }
    }

    private void Update()
    {
        if (!IsGrounded())
            return;

        float speed = GetNormalizedSpeed();

        if (speed < MIN_SPEED_THRESHOLD)
        {
            _stepTimer = 0f;
            return;
        }

        float interval = _BaseStepInterval / speed;

        _stepTimer += Time.deltaTime;

        if (_stepTimer >= interval)
        {
            _stepTimer = 0f;
            PlayFootstepVFX();
        }
    }

    private void PlayFootstepVFX()
    {
        VFXType vfx = IsOnWater()
            ? _WaterStepVFX
            : _GroundStepVFX;

        Mb_VFXManager.Play(vfx, transform.position);
    }

    private void HandleJump()
    {
        Vector3 spawnPos = transform.position + Vector3.up * _JumpLandHeightOffset;
        Mb_VFXManager.Play(_JumpVFX, spawnPos);
    }

    private void HandleLand()
    {
        Vector3 spawnPos = transform.position + Vector3.up * _JumpLandHeightOffset;
        Mb_VFXManager.Play(_LandVFX, spawnPos);
    }

    private bool IsGrounded()
    {
        if (_controller != null)
            return _controller.isGrounded;

        return Physics.Raycast(
            transform.position,
            Vector3.down,
            _SurfaceCheckDistance
        );
    }

    private bool IsOnWater()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            0.2f,
            LayerMask.GetMask(_WaterLayerName),
            QueryTriggerInteraction.Collide
        );

        return hits.Length > 0;
    }

    private float GetNormalizedSpeed()
    {
        if (_character == null)
            return 0f;

        if (_controller != null)
        {
            Vector3 flatVelocity = new Vector3(
                _controller.velocity.x,
                0f,
                _controller.velocity.z
            );

            float maxSpeed = _character.Stats.MoveSpeed.GetValue();

            return maxSpeed > 0f
                ? Mathf.Clamp01(flatVelocity.magnitude / maxSpeed)
                : 0f;
        }

        return 0f;
    }
}