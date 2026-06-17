// Mb_FootstepAudio.cs
// Plays footstep sounds timed to the character's movement speed.
// Detects whether the character is on water or ground using a downward
// raycast that checks the tag of whatever surface is below.
//
// WHY TAG-BASED DETECTION:
//   Water is a separate GameObject from terrain in this project. Tagging
//   the water surface "Water" and everything else as default terrain means
//   a single Raycast gives us the surface type with no extra components needed.
//   If you add more surface types later (wood, stone, etc.), add a tag and
//   a new CombatSFX enum entry — nothing else changes.
//
// HOW FOOTSTEP TIMING WORKS:
//   Rather than reading animation events (which requires animator setup),
//   we use a simple timer: footstep interval = BASE_INTERVAL / normalizedSpeed.
//   This means faster movement = faster footstep rate, which feels natural.
//   The timer only ticks when the character is grounded and moving.
//
// INSPECTOR SETUP:
//   - GroundStepSFX: CombatSFX enum for footstep on ground
//   - WaterStepSFX:  CombatSFX enum for footstep on water
//   - BaseStepInterval: seconds between steps at full speed (default 0.4s)
//   - SurfaceCheckDistance: raycast length downward (default 1.2f)
//   - WaterTag: tag assigned to your water surface GameObject (default "Water")

using UnityEngine;

public class Mb_FootstepAudio : MonoBehaviour
{
    [Header("Footstep SFX")]
    [SerializeField] private EnvironmentSFX _GroundStepSFX = EnvironmentSFX.Guardian_Footstep_Generic;
    [SerializeField] private EnvironmentSFX _WaterStepSFX = EnvironmentSFX.Guardian_Footstep_Water;


    [Header("Timing")]
    // How many seconds between footstep sounds at full movement speed.
    // Lower = faster cadence. Tune to match your character's stride animation.
    [SerializeField] private float _BaseStepInterval = 0.4f;

    [Header("Surface Detection")]
    [SerializeField] private float _SurfaceCheckDistance = 1.2f;
    [SerializeField] private string _WaterTag = "Water";


    private Mb_CharacterBase _character;
    private CharacterController _controller;

    private float _stepTimer = 0f;

    // Minimum speed (normalized 0-1) before footsteps start playing.
    // Prevents a footstep sound when the character is barely drifting.
    private const float MIN_SPEED_THRESHOLD = 0.1f;


    private void Awake()
    {
        _character = GetComponent<Mb_CharacterBase>();
        _controller = GetComponent<CharacterController>();

        if (_character == null)
            Debug.LogError($"[Mb_FootstepAudio] No Mb_CharacterBase on {gameObject.name}.");
    }

    private void Update()
    {
        // Only tick the footstep timer when grounded and actually moving
        if (!IsGrounded()) return;

        float speed = GetNormalizedSpeed();
        if (speed < MIN_SPEED_THRESHOLD)
        {
            // Standing still — reset timer so the first step after
            // moving again isn't delayed by leftover time
            _stepTimer = 0f;
            return;
        }

        // Speed scales the cadence — moving faster shortens the interval
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


    // ─────────────────────────────────────────────────────────────────────────
    // Surface and Movement Detection
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsGrounded()
    {
        if (_controller != null)
            return _controller.isGrounded;

         //Fallback for characters without a CharacterController (e.g. CuBots on NavMesh)
         //A short downward raycast works as a grounded check
        return Physics.Raycast(transform.position, Vector3.down, _SurfaceCheckDistance);
    }


    private bool IsOnWater()
    {
        // Water collider is a trigger — CheckSphere ignores triggers by default.
        // OverlapSphere with QueryTriggerInteraction.Collide detects them explicitly.
        Vector3 checkOrigin = transform.position;

        Collider[] hits = Physics.OverlapSphere(
            checkOrigin,
            0.2f,
            LayerMask.GetMask("Water"),
            QueryTriggerInteraction.Collide
        );

        //Debug.Log($"Checking for water under {gameObject.name}. Hits: {hits.Length}");

        return hits.Length > 0;
    }


    private float GetNormalizedSpeed()
    {
        if (_character == null) return 0f;

        // Compare actual XZ velocity against the character's max move speed
        // to get a 0-1 normalized value — same as what the animator uses
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