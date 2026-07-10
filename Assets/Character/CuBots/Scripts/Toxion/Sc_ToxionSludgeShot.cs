using UnityEngine;

/// <summary>
/// Toxion's sludge shot - lobs a toxic projectile that creates a damaging sludge zone.
/// </summary>
public class Sc_ToxionSludgeShot : Sc_BaseAbility
{
    private const float AIM_HEIGHT_OFFSET = 1.0f;
    private const float UPWARD_AIM_ANGLE = 18.0f;
    private const float PROJECTILE_SPEED = 18.0f;
    private const float PROJECTILE_LIFETIME = 4.0f;
    private const float SLUDGE_ZONE_DURATION = 3.0f;
    private const float SLUDGE_ZONE_RADIUS = 2.5f;
    private const float SLUDGE_TICK_INTERVAL = 0.5f;

    private readonly System.Func<Transform> _getCurrentTarget;
    private GameObject _projectilePrefab;
    private GameObject _sludgeZonePrefab;

    public Sc_ToxionSludgeShot(
        SO_Ability abilityData,
        Mb_CharacterBase user,
        System.Func<Transform> getCurrentTarget)
        : base(abilityData, user)
    {
        _getCurrentTarget = getCurrentTarget;

        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Toxion_SludgeProjectile);
        _sludgeZonePrefab = registry?.GetPrefab(AbilityPrefabID.Toxion_SludgeZone);

        if (_projectilePrefab == null)
            _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Hunter_BulletProjectile);

        if (_projectilePrefab == null)
            _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Generic_Projectile);

        if (_sludgeZonePrefab == null)
            Debug.LogError($"[Sc_ToxionSludgeShot] Toxion_SludgeZone prefab is not assigned on {user.gameObject.name}.");
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

        float impactDamage = _AbilityData.GetStat(
            "ImpactDamage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue()
        );
        impactDamage = ApplyCriticalStrike(impactDamage, user);

        float sludgeDamagePerTick = _AbilityData.GetStat(
            "SludgeDamagePerTick",
            CurrentLevel,
            ap: user.Stats.AbilityPower.GetValue()
        );

        float slowPercent = _AbilityData.GetStat(
            "SlowPercent",
            CurrentLevel,
            ap: user.Stats.AbilityPower.GetValue()
        );

        SpawnProjectile(
            user,
            fireDirection,
            impactDamage,
            sludgeDamagePerTick,
            slowPercent
        );
    }

    private void SpawnProjectile(
        Mb_CharacterBase user,
        Vector3 fireDirection,
        float impactDamage,
        float sludgeDamagePerTick,
        float slowPercent)
    {
        GameObject projectileObject = _projectilePrefab != null
            ? Object.Instantiate(_projectilePrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        projectileObject.name = "Toxion Sludge Projectile";
        projectileObject.transform.SetPositionAndRotation(
            user.transform.position + Vector3.up * 1.0f + user.transform.forward * 0.6f,
            Quaternion.LookRotation(fireDirection)
        );

        Collider projectileCollider = projectileObject.GetComponent<Collider>();
        if (projectileCollider == null)
            projectileCollider = projectileObject.AddComponent<SphereCollider>();

        projectileCollider.isTrigger = true;

        Rigidbody projectileBody = projectileObject.GetComponent<Rigidbody>();
        if (projectileBody == null)
            projectileBody = projectileObject.AddComponent<Rigidbody>();

        projectileBody.useGravity = true;
        projectileBody.isKinematic = false;
        projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Mb_ToxionSludgeProjectile sludgeProjectile =
            projectileObject.GetComponent<Mb_ToxionSludgeProjectile>();

        if (sludgeProjectile == null)
            sludgeProjectile = projectileObject.AddComponent<Mb_ToxionSludgeProjectile>();

        sludgeProjectile.Initialize(
            user,
            impactDamage,
            sludgeDamagePerTick,
            slowPercent,
            SLUDGE_ZONE_DURATION,
            SLUDGE_ZONE_RADIUS,
            SLUDGE_TICK_INTERVAL,
            PROJECTILE_LIFETIME,
            _sludgeZonePrefab
        );

        projectileBody.linearVelocity = fireDirection.normalized * PROJECTILE_SPEED;
    }
}
