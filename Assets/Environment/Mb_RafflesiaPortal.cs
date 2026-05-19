// Mb_RafflesiaPortal.cs
// Two Rafflesia flowers in Stage 1 act as paired teleport portals for the Guardian.
// Touching one instantly transports the Guardian to the other, then ejects them
// forward-upward from the destination to prevent an immediate bounce-back.
//
// HOW IT WORKS:
//   1. Guardian enters this portal's trigger → we check it's not on cooldown.
//   2. We disable the Guardian's CharacterController, set transform.position to
//      the destination portal's position, then re-enable the CharacterController.
//      (Disabling CC first avoids the one-frame desync where the CC fights the
//      position set and snaps the character back.)
//   3. We call Mb_Movement.StartDash() with an ejection vector built from the
//      destination portal's forward direction angled upward by EjectionAngle.
//      This uses the existing movement system instead of setting velocity directly.
//   4. The DESTINATION portal starts its cooldown — it won't trigger again until
//      the cooldown expires. The SOURCE portal has no cooldown.
//
// WHY DESTINATION-ONLY COOLDOWN:
//   After teleporting, the Guardian lands near the destination portal and could
//   immediately walk back into it. The cooldown window gives them time to move
//   away. The source portal stays open — intentional re-use is fine.
//
// Inspector setup:
//   - Attach this script to each Rafflesia flower GameObject.
//   - Add a Trigger Collider (Is Trigger = true) sized to the flower — a Sphere
//     Collider usually works well for a flower-shaped object.
//   - PairedPortal: drag the OTHER Rafflesia's GameObject here. Both portals
//     must reference each other.
//   - EjectionSpeed: how fast the post-teleport dash moves. Default 8f.
//     // TODO: Tune — higher values push the Guardian further from the portal.
//   - EjectionAngle: degrees above horizontal for the ejection arc. Default 30f.
//     // TODO: Tune — 0 = flat dash, 90 = straight up. 20–40 feels natural.
//   - DestinationCooldown: seconds before the destination portal can fire again.
//     Default 1.5f. // TODO: Tune based on EjectionSpeed — longer dash = shorter
//     cooldown needed since the Guardian lands further away.

using System.Collections;
using UnityEngine;

public class Mb_RafflesiaPortal : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Pairing")]
    [Tooltip("The other Rafflesia portal. Drag the paired portal's GameObject here.")]
    [SerializeField] private Mb_RafflesiaPortal PairedPortal;

    [Header("Ejection")]
    [Tooltip("Speed of the post-teleport dash in units per second.")]
    [SerializeField]
    // TODO: Tune — 8f is a gentle push. Increase if the Guardian lands too close to the portal.
    private float EjectionSpeed = 8f;

    [Tooltip("Degrees above horizontal for the ejection arc. 0 = flat, 90 = straight up.")]
    [SerializeField]
    // TODO: Tune — 30f gives a slight hop that feels like emerging from a flower.
    private float EjectionAngle = 30f;

    [Header("Cooldown")]
    [Tooltip("Seconds this portal ignores triggers after being used as a destination.")]
    [SerializeField]
    // TODO: Tune alongside EjectionSpeed — the Guardian should land outside trigger
    // range before the cooldown expires.
    private float DestinationCooldown = 1.5f;


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    // True while this portal is on cooldown as a destination.
    // Set by the SOURCE portal after it teleports the Guardian here.
    private bool _isOnCooldown = false;


    // -------------------------------------------------------------------------
    // Trigger Detection
    // -------------------------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        // Only the Guardian can use portals — CuBots pass through unaffected.
        // We check for Mb_GuardianBase specifically because CuBots also derive
        // from Mb_CharacterBase, so a CharacterBase check would catch both.
        Mb_GuardianBase guardian = other.GetComponent<Mb_GuardianBase>();
        if (guardian == null) return;

        // This portal is on destination cooldown — the Guardian just arrived here
        // and hasn't cleared the trigger yet. Skip to avoid an instant bounce-back.
        if (_isOnCooldown) return;

        // Safety check — can't teleport without a destination
        if (PairedPortal == null)
        {
            Debug.LogWarning($"[Mb_RafflesiaPortal] {gameObject.name} has no PairedPortal assigned.");
            return;
        }

        TeleportGuardian(guardian);
    }


    // -------------------------------------------------------------------------
    // Teleport Logic
    // -------------------------------------------------------------------------

    private void TeleportGuardian(Mb_GuardianBase guardian)
    {
        // --- Step 1: Grab references we need ---
        CharacterController cc = guardian.GetComponent<CharacterController>();
        Mb_Movement movement = guardian.Movement;

        if (movement == null)
        {
            Debug.LogError($"[Mb_RafflesiaPortal] Guardian has no Mb_Movement component.");
            return;
        }

        // --- Step 2: Disable CharacterController before moving ---
        // If we set transform.position while the CC is active, the CC's internal
        // state still thinks the character is at the old position and will fight
        // the move for one frame, causing a visible snap or stutter.
        // Disabling it first lets us set position cleanly.
        if (cc != null) cc.enabled = false;

        // --- Step 3: Move Guardian to destination portal's position ---
        guardian.transform.position = PairedPortal.transform.position;

        // --- Step 4: Re-enable CharacterController at the new position ---
        // The CC rebuilds its internal position from transform.position on re-enable,
        // so it is now in sync with where we placed the Guardian.
        if (cc != null) cc.enabled = true;

        // --- Step 5: Eject the Guardian away from the destination portal ---
        // Build an ejection vector from the destination's forward direction,
        // rotated upward by EjectionAngle. This gives a natural "emerging from
        // the flower" arc rather than a flat ground dash.
        Vector3 ejectionDir = BuildEjectionDirection(PairedPortal.transform.forward);
        float ejectionDuration = CalculateEjectionDuration();

        // StartDash expects a velocity vector (direction × speed), not just direction.
        movement.StartDash(ejectionDir * EjectionSpeed, ejectionDuration);

        // --- Step 6: Put the DESTINATION portal on cooldown ---
        // Source portal (this one) stays open. Only the destination needs a cooldown
        // to prevent the Guardian from immediately triggering it after landing.
        PairedPortal.StartCooldown(DestinationCooldown);

        Debug.Log($"[Mb_RafflesiaPortal] Guardian teleported from {gameObject.name} " +
                  $"to {PairedPortal.gameObject.name}.");
    }


    // -------------------------------------------------------------------------
    // Ejection Helpers
    // -------------------------------------------------------------------------

    // Rotates the portal's forward direction upward by EjectionAngle degrees.
    // Example: forward = (0,0,1), angle = 30° → result points forward and slightly up.
    // We rotate around the portal's RIGHT axis so the arc stays in the forward plane.
    private Vector3 BuildEjectionDirection(Vector3 portalForward)
    {
        // Flatten the forward vector first — we don't want a portal tilted in the
        // terrain to fire the Guardian sideways into the ground.
        Vector3 flatForward = new Vector3(portalForward.x, 0f, portalForward.z).normalized;

        // Fallback if the portal is pointing straight up (degenerate case)
        if (flatForward == Vector3.zero)
            flatForward = Vector3.forward;

        // Rotate the flat forward upward by EjectionAngle around the portal's right axis.
        // Quaternion.AngleAxis(angle, axis) * vector = vector rotated around axis by angle.
        Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;
        Vector3 ejectionDir = Quaternion.AngleAxis(-EjectionAngle, right) * flatForward;

        return ejectionDir.normalized;
    }


    // The dash duration is how long the Guardian is pushed by the ejection.
    // We calculate it so the Guardian travels roughly 2 character-lengths before
    // normal movement resumes — short enough to feel snappy, long enough to clear
    // the destination trigger collider.
    // TODO: If EjectionSpeed is tuned significantly, revisit this formula.
    //       A very fast ejection with a long duration sends the Guardian too far.
    private float CalculateEjectionDuration()
    {
        // Target travel distance of ~2 units — just enough to exit the trigger.
        // Duration = distance / speed. Clamped so it never exceeds 0.4s regardless
        // of how slow EjectionSpeed is tuned.
        const float TARGET_CLEAR_DISTANCE = 2f;
        const float MAX_DURATION = 0.4f;

        if (EjectionSpeed <= 0f) return MAX_DURATION;

        return Mathf.Min(TARGET_CLEAR_DISTANCE / EjectionSpeed, MAX_DURATION);
    }


    // -------------------------------------------------------------------------
    // Cooldown (called by the SOURCE portal on this portal after a teleport)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Puts this portal on cooldown so it cannot fire again immediately.
    /// Called by the partner portal after it teleports the Guardian here.
    /// </summary>
    public void StartCooldown(float duration)
    {
        StartCoroutine(CooldownRoutine(duration));
    }


    private IEnumerator CooldownRoutine(float duration)
    {
        _isOnCooldown = true;

        Debug.Log($"[Mb_RafflesiaPortal] {gameObject.name} destination cooldown started " +
                  $"({duration}s).");

        yield return new WaitForSeconds(duration);

        _isOnCooldown = false;

        Debug.Log($"[Mb_RafflesiaPortal] {gameObject.name} destination cooldown ended.");
    }


    // -------------------------------------------------------------------------
    // Scene Cleanup
    // -------------------------------------------------------------------------

    private void OnDisable()
    {
        // Clear cooldown state if the portal is disabled mid-stage (e.g. stage teardown).
        // This ensures no stale coroutines leave _isOnCooldown = true if the portal
        // is re-enabled in a future scene load.
        _isOnCooldown = false;
        StopAllCoroutines();
    }
}