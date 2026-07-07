using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toxic sludge area created by Toxion's projectile impact.
/// </summary>
public class Mb_ToxionSludgeZone : MonoBehaviour
{
    private readonly Dictionary<Mb_CharacterBase, Coroutine> _activeTargets = new();

    private float _damagePerTick;
    private float _slowPercent;
    private float _duration;
    private float _tickInterval;

    public void Initialize(
        float damagePerTick,
        float slowPercent,
        float duration,
        float tickInterval)
    {
        _damagePerTick = damagePerTick;
        _slowPercent = Mathf.Clamp(slowPercent, 0f, 0.9f);
        _duration = duration;
        _tickInterval = tickInterval;

        StartCoroutine(LifetimeRoutine());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Mb_CharacterBase target = other.GetComponent<Mb_CharacterBase>();
        if (target == null) return;
        if (_activeTargets.ContainsKey(target)) return;

        Coroutine routine = StartCoroutine(ContactRoutine(target));
        _activeTargets[target] = routine;
    }

    private void OnTriggerExit(Collider other)
    {
        Mb_CharacterBase target = other.GetComponent<Mb_CharacterBase>();
        if (target == null) return;

        StopTrackingTarget(target);
    }

    private IEnumerator ContactRoutine(Mb_CharacterBase target)
    {
        WaitForSeconds wait = new WaitForSeconds(_tickInterval);

        while (target != null && target.Health != null && !target.Health.IsDead)
        {
            ApplyContactEffects(target);
            yield return wait;
        }
    }

    private void ApplyContactEffects(Mb_CharacterBase target)
    {
        Mb_StatusEffectController statusController =
            target.GetComponent<Mb_StatusEffectController>();

        if (statusController == null)
        {
            I_Damageable damageable = target.GetComponent<I_Damageable>();
            if (damageable != null)
                damageable.TakeDamage(_damagePerTick);

            return;
        }

        float statusDuration = _tickInterval + 0.15f;
        statusController.Apply(Sc_StatusEffect.Poison(statusDuration, _damagePerTick, _tickInterval));
        statusController.Apply(Sc_StatusEffect.MoveSlow(statusDuration, _slowPercent));
        statusController.Apply(Sc_StatusEffect.AttackSlow(statusDuration, _slowPercent));
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(_duration);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<Mb_CharacterBase, Coroutine> targetRoutine in _activeTargets)
        {
            if (targetRoutine.Value != null)
                StopCoroutine(targetRoutine.Value);
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
