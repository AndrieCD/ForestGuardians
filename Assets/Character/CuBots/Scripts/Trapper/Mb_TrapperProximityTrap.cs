using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime proximity trap dropped by Trapper.
/// </summary>
public class Mb_TrapperProximityTrap : MonoBehaviour
{
    private float _damage;
    private float _slowPercent;
    private float _slowDuration;
    private bool _hasTriggered;

    private void Awake()
    {
        Collider trapCollider = GetComponent<Collider>();
        if (trapCollider == null)
            trapCollider = GetComponentInChildren<Collider>();

        if (trapCollider == null)
        {
            Debug.LogError($"[Mb_TrapperProximityTrap] No Collider found on {gameObject.name}. Add a trigger collider to the trap prefab.");
        }
        else if (!trapCollider.isTrigger)
        {
            trapCollider.isTrigger = true;
        }

        Rigidbody trapBody = GetComponent<Rigidbody>();
        if (trapBody == null)
            trapBody = gameObject.AddComponent<Rigidbody>();

        trapBody.isKinematic = true;
        trapBody.useGravity = false;
    }

    public void Initialize(
        float damage,
        float slowPercent,
        float slowDuration,
        float lifetime)
    {
        _damage = damage;
        _slowPercent = slowPercent;
        _slowDuration = slowDuration;
        _hasTriggered = false;

        StartCoroutine(LifetimeRoutine(lifetime));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;
        if (!other.CompareTag("Player")) return;

        I_Damageable damageable = other.GetComponent<I_Damageable>();
        if (damageable == null) return;

        _hasTriggered = true;
        damageable.TakeDamage(_damage);

        Mb_CharacterBase target = other.GetComponent<Mb_CharacterBase>();
        Mb_StatusEffectController statusController = target != null
            ? target.GetComponent<Mb_StatusEffectController>()
            : null;

        if (statusController != null)
        {
            float slowFraction = Mathf.Clamp(_slowPercent, 0f, 100f) / 100f;
            statusController.Apply(Sc_StatusEffect.MoveSlow(_slowDuration, slowFraction));
        }

        Destroy(gameObject);
    }

    private IEnumerator LifetimeRoutine(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        if (gameObject != null)
            Destroy(gameObject);
    }
}
