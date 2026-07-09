using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drilly's burrow strike.
/// The target position is snapshotted after windup so the launch does not
/// continue tracking the target during travel.
/// </summary>
public class Sc_DrillyBurrowStrike : Sc_BaseAbility
{
    private readonly Func<Transform> _getCurrentTarget;
    private readonly Action _onSurface;
    private readonly float _windupDuration;
    private readonly float _launchDuration;
    private readonly float _impactRadius;

    public Sc_DrillyBurrowStrike(
        SO_Ability abilityData,
        Mb_CharacterBase user,
        Func<Transform> getCurrentTarget,
        Action onSurface,
        float windupDuration,
        float launchDuration,
        float impactRadius)
        : base(abilityData, user)
    {
        _getCurrentTarget = getCurrentTarget;
        _onSurface = onSurface;
        _windupDuration = Mathf.Max(0f, windupDuration);
        _launchDuration = Mathf.Max(0.01f, launchDuration);
        _impactRadius = Mathf.Max(0.1f, impactRadius);
    }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;
        if (user == null || user.Health == null || user.Health.IsDead) return;

        user.StartCoroutine(BurrowStrikeRoutine(user));
        StartCooldown(user, GetAttackCooldown(user));
    }

    private IEnumerator BurrowStrikeRoutine(Mb_CharacterBase user)
    {
        _onSurface?.Invoke();

        Mb_CuBotAnimator animator = user.GetComponent<Mb_CuBotAnimator>();
        animator?.TriggerAttack();

        if (_windupDuration > 0f)
            yield return new WaitForSeconds(_windupDuration);

        Transform target = _getCurrentTarget?.Invoke();
        Vector3 launchTarget = target != null
            ? target.position
            : user.transform.position + user.transform.forward;

        yield return LaunchToPosition(user, launchTarget);

        ApplyImpactDamage(user);
        SelfDestruct(user);
    }

    private IEnumerator LaunchToPosition(Mb_CharacterBase user, Vector3 launchTarget)
    {
        NavMeshAgent agent = user.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.isStopped = true;
        }

        Vector3 start = user.transform.position;
        Vector3 end = new Vector3(launchTarget.x, start.y, launchTarget.z);
        Vector3 direction = end - start;

        if (direction.sqrMagnitude > 0.001f)
            user.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        float elapsed = 0f;
        while (elapsed < _launchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _launchDuration);
            MoveUser(user, agent, Vector3.Lerp(start, end, t));
            yield return null;
        }

        MoveUser(user, agent, end);
    }

    private void MoveUser(Mb_CharacterBase user, NavMeshAgent agent, Vector3 position)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Warp(position);
            return;
        }

        user.transform.position = position;
    }

    private void ApplyImpactDamage(Mb_CharacterBase user)
    {
        float damage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue()
        );

        damage = ApplyCriticalStrike(damage, user);

        Collider[] hits = Physics.OverlapSphere(
            user.transform.position + Vector3.up,
            _impactRadius
        );

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == user.gameObject) continue;
            if (hit.gameObject.CompareTag("CuBot")) continue;

            I_Damageable damageable = hit.GetComponentInParent<I_Damageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(damage);
            break;
        }
    }

    private void SelfDestruct(Mb_CharacterBase user)
    {
        if (user.Health == null || user.Health.IsDead) return;

        user.Health.IsUntargetable = false;
        user.Health.TakeDamage(user.Health.CurrentHealth);
    }
}
