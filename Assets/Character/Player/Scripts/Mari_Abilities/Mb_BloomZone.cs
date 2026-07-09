using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// Mb_BloomZone.cs
// Spherical trigger zone for Psychic Bloom's active.
// =============================================================================

public class Mb_BloomZone : MonoBehaviour
{
    private const float EXPIRE_DESTROY_DELAY = 1.5f;

    private Mb_CharacterBase _owner;
    private float _damage;
    private float _tickInterval;
    private float _slowPercent;
    private float _slowDuration;
    private float _duration;
    private bool _isDeactivated;

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
        _duration = Mathf.Max(0f, duration);
        _isDeactivated = false;

        float safeRadius = Mathf.Max(0.01f, radius);

        if (_owner != null)
        {
            transform.SetParent(_owner.transform, false);
            transform.localPosition = Vector3.up * (safeRadius * 0.5f);
            transform.localRotation = Quaternion.identity;
        }

        transform.localScale = Vector3.one * (safeRadius * 2f);

        _collider = GetComponent<SphereCollider>();
        if (_collider != null)
        {
            _collider.isTrigger = true;
            _collider.radius = 0.5f;
            _collider.center = Vector3.zero;
            _collider.enabled = true;
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
            _visualRoot.localScale = Vector3.one;

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
        if (_isDeactivated) return;
        if (_owner != null && other.gameObject == _owner.gameObject) return;

        MB_CuBotBase enemy = other.GetComponentInParent<MB_CuBotBase>();
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
        MB_CuBotBase enemy = other.GetComponentInParent<MB_CuBotBase>();
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
        if (_isDeactivated) return;

        _isDeactivated = true;

        if (_collider != null)
            _collider.enabled = false;

        if (_fieldVFX != null)
            _fieldVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        _tickTimers.Clear();
        Destroy(gameObject, EXPIRE_DESTROY_DELAY);
    }
}
