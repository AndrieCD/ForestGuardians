using UnityEngine;

public class Mb_FootstepAudio : MonoBehaviour
{
    [Header("Footstep SFX")]
    [SerializeField] private EnvironmentSFX _GroundStepSFX = EnvironmentSFX.Guardian_Footstep_Generic;
    [SerializeField] private EnvironmentSFX _WaterStepSFX = EnvironmentSFX.Guardian_Footstep_Water;

    [Header("Jump / Land SFX")]
    [SerializeField] private EnvironmentSFX _JumpSFX = EnvironmentSFX.Guardian_Jump_Generic;
    [SerializeField] private EnvironmentSFX _LandSFX = EnvironmentSFX.Guardian_Land_Generic;

    [Header("Timing")]
    [SerializeField] private float _BaseStepInterval = 0.4f;

    [Header("Surface Detection")]
    [SerializeField] private float _SurfaceCheckDistance = 1.2f;
    [SerializeField] private string _WaterTag = "Water";


    private Mb_CharacterBase _character;
    private CharacterController _controller;
    private Mb_Movement _movement;

    private float _stepTimer = 0f;

    private const float MIN_SPEED_THRESHOLD = 0.1f;


    private void Awake()
    {
        _character = GetComponent<Mb_CharacterBase>();
        _controller = GetComponent<CharacterController>();
        _movement = GetComponent<Mb_Movement>();

        if (_character == null)
            Debug.LogError($"[Mb_FootstepAudio] No Mb_CharacterBase on {gameObject.name}.");

        if (_movement == null)
            Debug.LogError($"[Mb_FootstepAudio] No Mb_Movement on {gameObject.name}. " +
                           "Jump and land SFX will not play.");
    }


    private void OnEnable()
    {
        if (_movement != null)
        {
            Mb_Movement.OnLanded += HandleLand;
            Mb_Movement.OnJumped += HandleJump;
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
        if (!IsGrounded()) return;

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
            PlayFootstep();
        }
    }


    private void PlayFootstep()
    {
        EnvironmentSFX sfx = IsOnWater() ? _WaterStepSFX : _GroundStepSFX;
        Mb_AudioManager.PlayEnvironmentSFX(sfx, transform.position);
    }


    private void HandleJump()
    {
        if (!IsGrounded())
            Mb_AudioManager.PlayEnvironmentSFX(_JumpSFX, transform.position);
    }


    private void HandleLand()
    {
        Mb_AudioManager.PlayEnvironmentSFX(_LandSFX, transform.position);
    }


    // ── Surface and Movement Detection (unchanged) ───────────────────────────

    private bool IsGrounded()
    {
        if (_controller != null)
            return _controller.isGrounded;

        return Physics.Raycast(transform.position, Vector3.down, _SurfaceCheckDistance);
    }


    private bool IsOnWater()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            0.2f,
            LayerMask.GetMask("Water"),
            QueryTriggerInteraction.Collide
        );

        return hits.Length > 0;
    }


    private float GetNormalizedSpeed()
    {
        if (_character == null) return 0f;

        if (_controller != null)
        {
            Vector3 flatVelocity = new Vector3(
                _controller.velocity.x, 0f, _controller.velocity.z
            );
            float maxSpeed = _character.Stats.MoveSpeed.GetValue();
            return maxSpeed > 0f
                ? Mathf.Clamp01(flatVelocity.magnitude / maxSpeed)
                : 0f;
        }

        return 0f;
    }
}