// Mb_RafflesiaPortal.cs
// Paired Rafflesia flowers that teleport the Guardian between two trigger volumes.
// The destination portal briefly goes on cooldown to prevent immediate bounce-back.
//
// Inspector setup:
//   - Attach this script to each Rafflesia portal GameObject.
//   - Each portal needs a trigger collider.
//   - PairedPortal must reference the other portal in the pair.

using System.Collections;
using UnityEngine;

public class Mb_RafflesiaPortal : MonoBehaviour
{
    [Header("Pairing")]
    [Tooltip("The other Rafflesia portal. Drag the paired portal's GameObject here.")]
    [SerializeField] private Mb_RafflesiaPortal PairedPortal;

    [Header("Ejection")]
    [Tooltip("Speed of the post-teleport dash in units per second.")]
    [SerializeField] private float EjectionSpeed = 8f;

    [Tooltip("Degrees above horizontal for the ejection arc. 0 = flat, 90 = straight up.")]
    [SerializeField] private float EjectionAngle = 30f;

    [Header("Cooldown")]
    [Tooltip("Seconds this portal ignores triggers after being used as a destination.")]
    [SerializeField] private float DestinationCooldown = 1.5f;

    private bool _isOnCooldown = false;

    private void OnTriggerEnter(Collider other)
    {
        Mb_GuardianBase guardian = other.GetComponent<Mb_GuardianBase>();
        if (guardian == null) return;
        if (_isOnCooldown) return;

        if (PairedPortal == null)
        {
            Debug.LogWarning($"[Mb_RafflesiaPortal] {gameObject.name} has no PairedPortal assigned.");
            return;
        }

        TeleportGuardian(guardian);
    }

    private void TeleportGuardian(Mb_GuardianBase guardian)
    {
        CharacterController characterController = guardian.GetComponent<CharacterController>();
        Mb_Movement movement = guardian.Movement;

        if (movement == null)
        {
            Debug.LogError("[Mb_RafflesiaPortal] Guardian has no Mb_Movement component.");
            return;
        }

        if (characterController != null)
            characterController.enabled = false;

        guardian.transform.position = PairedPortal.transform.position;

        if (characterController != null)
            characterController.enabled = true;

        Vector3 ejectionDirection = BuildEjectionDirection(PairedPortal.transform.forward);
        float ejectionDuration = CalculateEjectionDuration();

        movement.StartDash(ejectionDirection * EjectionSpeed, ejectionDuration);
        PairedPortal.StartCooldown(DestinationCooldown);
    }

    private Vector3 BuildEjectionDirection(Vector3 portalForward)
    {
        Vector3 flatForward = new Vector3(portalForward.x, 0f, portalForward.z).normalized;

        if (flatForward == Vector3.zero)
            flatForward = Vector3.forward;

        Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;
        Vector3 ejectionDirection = Quaternion.AngleAxis(-EjectionAngle, right) * flatForward;

        return ejectionDirection.normalized;
    }

    private float CalculateEjectionDuration()
    {
        const float TARGET_CLEAR_DISTANCE = 2f;
        const float MAX_DURATION = 0.4f;

        if (EjectionSpeed <= 0f) return MAX_DURATION;

        return Mathf.Min(TARGET_CLEAR_DISTANCE / EjectionSpeed, MAX_DURATION);
    }

    /// <summary>
    /// Puts this portal on cooldown after it receives the Guardian from its paired portal.
    /// </summary>
    public void StartCooldown(float duration)
    {
        StartCoroutine(CooldownRoutine(duration));
    }

    private IEnumerator CooldownRoutine(float duration)
    {
        _isOnCooldown = true;
        yield return new WaitForSeconds(duration);
        _isOnCooldown = false;
    }

    private void OnDisable()
    {
        _isOnCooldown = false;
        StopAllCoroutines();
    }
}
