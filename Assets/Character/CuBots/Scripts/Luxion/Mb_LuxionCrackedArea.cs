using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Damaging cracked-ground area left by Luxion's Earth Breaker.
/// </summary>
public class Mb_LuxionCrackedArea : MonoBehaviour
{
    private readonly Dictionary<Mb_CharacterBase, Coroutine> _activeTargets = new();

    private float _damagePerTick;
    private float _tickInterval;
    private float _duration;

    public void Initialize(float damagePerTick, float tickInterval, float duration)
    {
        _damagePerTick = Mathf.Max(0f, damagePerTick);
        _tickInterval = Mathf.Max(0.1f, tickInterval);
        _duration = Mathf.Max(0.1f, duration);

        StartCoroutine(LifetimeRoutine());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Mb_CharacterBase target = other.GetComponentInParent<Mb_CharacterBase>();
        if (target == null) return;
        if (_activeTargets.ContainsKey(target)) return;

        _activeTargets[target] = StartCoroutine(DamageRoutine(target));
    }

    private void OnTriggerExit(Collider other)
    {
        Mb_CharacterBase target = other.GetComponentInParent<Mb_CharacterBase>();
        if (target == null) return;

        StopTrackingTarget(target);
    }

    private IEnumerator DamageRoutine(Mb_CharacterBase target)
    {
        WaitForSeconds wait = new WaitForSeconds(_tickInterval);

        while (target != null && target.Health != null && !target.Health.IsDead)
        {
            target.Health.TakeDamage(_damagePerTick);
            yield return wait;
        }
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(_duration);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<Mb_CharacterBase, Coroutine> activeTarget in _activeTargets)
        {
            if (activeTarget.Value != null)
                StopCoroutine(activeTarget.Value);
        }

        _activeTargets.Clear();
    }

    private void StopTrackingTarget(Mb_CharacterBase target)
    {
        if (!_activeTargets.TryGetValue(target, out Coroutine routine)) return;

        if (routine != null)
            StopCoroutine(routine);

        _activeTargets.Remove(target);
    }
}
