using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// Mb_BloomZone.cs
// Spherical trigger zone for Psychic Bloom's active.
// =============================================================================

public class Mb_BloomZone : MonoBehaviour
{
    private Mb_CharacterBase _owner;
    private float _damage;
    private float _tickInterval;
    private float _slowPercent;
    private float _slowDuration;
    private float _duration;

    private readonly Dictionary<MB_CuBotBase, float> _tickTimers
        = new Dictionary<MB_CuBotBase, float>();

    private SphereCollider _collider;
    private Transform _visualRoot;
    private ParticleSystem _fieldVFX;

    public void Initialize(
        Mb_CharacterBase owner,
        float damage,
        float tickInterval,
        float slowPercent,
        float slowDuration,
        float radius,
        float duration)
    {
        _owner = owner;
        _damage = damage;
        _tickInterval = tickInterval;
        _slowPercent = slowPercent;
        _slowDuration = slowDuration;
        _duration = duration;

        _collider = GetComponent<SphereCollider>();
        if (_collider != null)
        {
            _collider.isTrigger = true;
            _collider.radius = radius;
            _collider.center = Vector3.zero;
        }
        else
        {
            Debug.LogError("[Mb_BloomZone] No SphereCollider found on bloom zone prefab.");
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        _visualRoot = transform.Find("BloomVisualRoot");
        if (_visualRoot != null)
            _visualRoot.localScale = Vector3.one * (radius * 2f);

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "BloomFieldVFX")
            {
                _fieldVFX = ps;
                break;
            }
        }

        _fieldVFX?.Play();
        StartCoroutine(DurationRoutine());
    }

    private void OnTriggerStay(Collider other)
    {
        if (_owner != null && other.gameObject == _owner.gameObject) return;

        MB_CuBotBase enemy = other.GetComponent<MB_CuBotBase>();
        if (enemy == null) return;
        if (enemy.Health == null || enemy.Health.IsDead) return;

        float now = Time.time;
        if (_tickTimers.TryGetValue(enemy, out float lastTick))
        {
            if (now - lastTick < _tickInterval) return;
        }

        _tickTimers[enemy] = now;
        ApplyDamageAndSlow(enemy);
    }

    private void OnTriggerExit(Collider other)
    {
        MB_CuBotBase enemy = other.GetComponent<MB_CuBotBase>();
        if (enemy != null)
            _tickTimers.Remove(enemy);
    }

    private void ApplyDamageAndSlow(MB_CuBotBase enemy)
    {
        enemy.Health.TakeDamage(_damage);

        Mb_StatusEffectController statusController =
            enemy.GetComponent<Mb_StatusEffectController>();

        statusController?.Apply(Sc_StatusEffect.MoveSlow(_slowDuration, _slowPercent));

        Debug.Log($"[Mb_BloomZone] Hit {enemy.CharacterName} for {_damage} " +
                  $"and applied {_slowPercent}% slow.");
    }

    private IEnumerator DurationRoutine()
    {
        yield return new WaitForSeconds(_duration);
        DeactivateZone();
    }

    private void DeactivateZone()
    {
        if (_fieldVFX != null)
            _fieldVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        _tickTimers.Clear();
        StartCoroutine(DestroyAfterDelay(1.5f));
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        if (_collider != null)
            _collider.enabled = false;

        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
}
