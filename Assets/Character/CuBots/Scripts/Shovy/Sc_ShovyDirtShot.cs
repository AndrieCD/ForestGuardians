using UnityEngine;

/// <summary>
/// Shovy's dirt shot - fires a single arcing projectile that slows on hit.
/// </summary>
public class Sc_ShovyDirtShot : Sc_BaseAbility
{
    private const float AIM_HEIGHT_OFFSET = 1.0f;
    private const float UPWARD_AIM_ANGLE = 22.0f;

    private readonly SO_ProjectileData _projectileData;
    private readonly Mb_ProjectileLauncher _launcher;
    private readonly System.Func<Transform> _getCurrentTarget;
    private GameObject _projectilePrefab;

    public Sc_ShovyDirtShot(
        SO_Ability abilityData,
        Mb_CharacterBase user,
        System.Func<Transform> getCurrentTarget)
        : base(abilityData, user)
    {
        _getCurrentTarget = getCurrentTarget;
        _launcher = user.GetComponent<Mb_ProjectileLauncher>();

        if (_launcher == null)
            Debug.LogError($"[Sc_ShovyDirtShot] No Mb_ProjectileLauncher found on {user.gameObject.name}.");

        _projectileData = abilityData.ProjectileData;

        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Hunter_BulletProjectile);

        if (_projectilePrefab == null)
            _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Generic_Projectile);

        if (_projectileData == null)
            Debug.LogError($"[Sc_ShovyDirtShot] SO_Ability.ProjectileData is null on {abilityData.AbilityName}.");
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
        Vector3 flatDirection = targetPosition - user.transform.position;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude <= 0.01f)
            flatDirection = user.transform.forward;

        Vector3 fireDirection = Quaternion.AngleAxis(
            -UPWARD_AIM_ANGLE,
            user.transform.right
        ) * flatDirection.normalized;

        float damage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue()
        );
        damage = ApplyCriticalStrike(damage, user);

        Mb_Projectile projectile = _launcher.FireToward(
            _projectilePrefab,
            _projectileData,
            user,
            damage,
            fireDirection
        );

        if (projectile == null) return;

        Rigidbody projectileBody = projectile.GetComponent<Rigidbody>();
        if (projectileBody != null)
            projectileBody.useGravity = true;
    }
}
