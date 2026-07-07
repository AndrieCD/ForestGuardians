using UnityEngine;

/// <summary>
/// Trapper's ranged attack - fires a straight projectile that slows on hit.
/// </summary>
public class Sc_TrapperRangeAttack : Sc_BaseAbility
{
    private const float AIM_HEIGHT_OFFSET = 0.5f;

    private readonly SO_ProjectileData _projectileData;
    private readonly Mb_ProjectileLauncher _launcher;
    private readonly System.Func<Transform> _getCurrentTarget;
    private GameObject _projectilePrefab;

    public Sc_TrapperRangeAttack(
        SO_Ability abilityData,
        Mb_CharacterBase user,
        System.Func<Transform> getCurrentTarget)
        : base(abilityData, user)
    {
        _getCurrentTarget = getCurrentTarget;
        _launcher = user.GetComponent<Mb_ProjectileLauncher>();

        if (_launcher == null)
            Debug.LogError($"[Sc_TrapperRangeAttack] No Mb_ProjectileLauncher found on {user.gameObject.name}.");

        _projectileData = abilityData.ProjectileData;

        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Hunter_BulletProjectile);

        if (_projectilePrefab == null)
            _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Generic_Projectile);

        if (_projectileData == null)
            Debug.LogError($"[Sc_TrapperRangeAttack] SO_Ability.ProjectileData is null on {abilityData.AbilityName}.");
    }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        user.GetComponent<Mb_CuBotAnimator>()?.TriggerAttack();

        Shoot(user);

        StartCooldown(user, GetAttackCooldown(user));
    }

    private void Shoot(Mb_CharacterBase user)
    {
        if (_launcher == null || _projectileData == null) return;

        Transform currentTarget = _getCurrentTarget?.Invoke();
        if (currentTarget == null) return;

        Vector3 targetPosition = currentTarget.position + Vector3.up * AIM_HEIGHT_OFFSET;
        Vector3 fireDirection = (targetPosition - user.transform.position).normalized;

        float damage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue()
        );
        damage = ApplyCriticalStrike(damage, user);

        _launcher.FireToward(
            _projectilePrefab,
            _projectileData,
            user,
            damage,
            fireDirection
        );
    }
}
