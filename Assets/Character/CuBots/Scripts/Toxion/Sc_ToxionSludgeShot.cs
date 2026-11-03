using UnityEngine;

/// <summary>
/// Toxion's sludge shot - fires a direct projectile that creates a damaging sludge zone.
/// </summary>
public class Sc_ToxionSludgeShot : Sc_BaseAbility
{
    private const float AIM_HEIGHT_OFFSET = 1.0f;
    private const float SLUDGE_ZONE_DURATION = 3.0f;
    private const float SLUDGE_ZONE_RADIUS = 2.5f;
    private const float SLUDGE_TICK_INTERVAL = 0.5f;
    // Start and search well above/below the impact point so airborne targets
    // still resolve their pool onto the floor beneath them.
    private const float GROUND_RAYCAST_START_HEIGHT = 100.0f;
    private const float GROUND_RAYCAST_DISTANCE = 250.0f;
    private const float MIN_GROUND_NORMAL_Y = 0.7f;
    private const float GROUND_SURFACE_OFFSET = 0.03f;

    private readonly System.Func<Transform> _getCurrentTarget;
    private readonly Mb_ProjectileLauncher _launcher;
    private readonly SO_ProjectileData _projectileData;
    private GameObject _projectilePrefab;
    private GameObject _sludgeZonePrefab;

    public Sc_ToxionSludgeShot(
        SO_Ability abilityData,
        Mb_CharacterBase user,
        System.Func<Transform> getCurrentTarget)
        : base(abilityData, user)
    {
        _getCurrentTarget = getCurrentTarget;
        _launcher = user.GetComponent<Mb_ProjectileLauncher>();
        _projectileData = abilityData.ProjectileData;

        if (_launcher == null)
            Debug.LogError($"[Sc_ToxionSludgeShot] No Mb_ProjectileLauncher found on {user.gameObject.name}.");

        if (_projectileData == null)
            Debug.LogError($"[Sc_ToxionSludgeShot] SO_ProjectileData is not assigned on {abilityData.AbilityName}.");

        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Toxion_SludgeProjectile);
        _sludgeZonePrefab = registry?.GetPrefab(AbilityPrefabID.Toxion_SludgeZone);

        if (_projectilePrefab == null)
            Debug.LogError($"[Sc_ToxionSludgeShot] Toxion_SludgeProjectile prefab is not assigned on {user.gameObject.name}.");
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
        if (_launcher == null || _projectileData == null || _projectilePrefab == null)
            return;

        Transform currentTarget = _getCurrentTarget?.Invoke();
        if (currentTarget == null) return;

        Vector3 targetPosition = currentTarget.position + Vector3.up * AIM_HEIGHT_OFFSET;
        Vector3 fireDirection = (
            targetPosition - _launcher.GetLaunchOriginPosition()
        ).normalized;

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

        _launcher.FireToward(
            _projectilePrefab,
            _projectileData,
            user,
            impactDamage,
            fireDirection,
            projectile =>
            {
                bool sludgeZoneSpawned = false;

                void SpawnOnce(Vector3 impactPoint)
                {
                    if (sludgeZoneSpawned) return;

                    sludgeZoneSpawned = true;
                    SpawnSludgeZoneOnGround(
                        impactPoint,
                        sludgeDamagePerTick,
                        slowPercent);
                }

                // OnHit is the reliable path for character impacts.
                projectile.OnHit += (_, impactPoint, _) => SpawnOnce(impactPoint);

                // OnImpact handles walls and ground colliders that are not damageable.
                projectile.OnImpact += (_, impactPoint, _) => SpawnOnce(impactPoint);
            });
    }

    private void SpawnSludgeZoneOnGround(
        Vector3 impactPoint,
        float sludgeDamagePerTick,
        float slowPercent)
    {
        if (_sludgeZonePrefab == null || !TryFindGroundPoint(impactPoint, out Vector3 groundPoint))
            return;

        if (IsOverlappingSludgeZone(groundPoint))
            return;

        GameObject zone = Object.Instantiate(_sludgeZonePrefab);
        zone.name = "Toxion Sludge Zone";
        zone.transform.SetPositionAndRotation(groundPoint, Quaternion.identity);
        zone.transform.localScale = new Vector3(
            SLUDGE_ZONE_RADIUS * 2.0f,
            0.1f,
            SLUDGE_ZONE_RADIUS * 2.0f);

        int areaEffectLayer = LayerMask.NameToLayer(Mb_ToxionSludgeZone.AREA_EFFECT_LAYER_NAME);
        if (areaEffectLayer >= 0)
            zone.layer = areaEffectLayer;

        Collider zoneCollider = zone.GetComponentInChildren<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        Rigidbody zoneBody = zone.GetComponent<Rigidbody>();
        if (zoneBody == null)
            zoneBody = zone.AddComponent<Rigidbody>();

        zoneBody.isKinematic = true;
        zoneBody.useGravity = false;

        Mb_ToxionSludgeZone sludgeZone = zone.GetComponent<Mb_ToxionSludgeZone>();
        if (sludgeZone == null)
            sludgeZone = zone.AddComponent<Mb_ToxionSludgeZone>();

        sludgeZone.Initialize(
            sludgeDamagePerTick,
            slowPercent,
            SLUDGE_ZONE_DURATION,
            SLUDGE_TICK_INTERVAL);
    }

    private bool TryFindGroundPoint(Vector3 impactPoint, out Vector3 groundPoint)
    {
        Vector3 rayOrigin = impactPoint + Vector3.up * GROUND_RAYCAST_START_HEIGHT;
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            GROUND_RAYCAST_DISTANCE,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || IsCharacterCollider(hit.collider))
                continue;

            if (hit.normal.y < MIN_GROUND_NORMAL_Y)
                continue;

            groundPoint = hit.point + hit.normal * GROUND_SURFACE_OFFSET;
            return true;
        }

        groundPoint = default;
        return false;
    }

    private bool IsCharacterCollider(Collider collider)
    {
        // Character colliders may live on child objects whose tag is not
        // inherited from the character root. Check the hierarchy first so an
        // airborne Guardian can never be mistaken for the ground surface.
        if (collider.GetComponentInParent<Mb_CharacterBase>() != null)
            return true;

        return collider.CompareTag("Player") ||
               collider.CompareTag("CuBot") ||
               collider.CompareTag("Panoharra");
    }

    private bool IsOverlappingSludgeZone(Vector3 position)
    {
        int areaEffectLayer = LayerMask.NameToLayer(Mb_ToxionSludgeZone.AREA_EFFECT_LAYER_NAME);
        if (areaEffectLayer < 0) return false;

        Collider[] overlaps = Physics.OverlapBox(
            position,
            new Vector3(SLUDGE_ZONE_RADIUS, 0.5f, SLUDGE_ZONE_RADIUS),
            Quaternion.identity,
            1 << areaEffectLayer,
            QueryTriggerInteraction.Collide);

        foreach (Collider overlap in overlaps)
        {
            if (overlap != null && overlap.GetComponentInParent<Mb_ToxionSludgeZone>() != null)
                return true;
        }

        return false;
    }
}
