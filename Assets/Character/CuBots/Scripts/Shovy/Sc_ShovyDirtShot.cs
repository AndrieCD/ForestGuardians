using UnityEngine;

/// <summary>
/// Shovy's dirt shot - fires a single arcing projectile that slows on hit.
///
/// ARC FIX: previously used a fixed UPWARD_AIM_ANGLE (22 degrees), which only
/// lands correctly at one specific distance. Now uses Sc_ProjectileArcSolver
/// to calculate the exact angle needed to hit the target at the projectile's
/// fixed LaunchSpeed, given the real horizontal/vertical distance each shot.
/// </summary>
public class Sc_ShovyDirtShot : Sc_BaseAbility
{
    private const float AIM_HEIGHT_OFFSET = 1.0f;

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

        // Muzzle position — matches where FireToward() will actually spawn the
        // projectile (LaunchOrigin on the launcher), so the solve is accurate.
        // We approximate it here with the user's position + a small forward/up
        // offset since the launcher's exact LaunchOrigin isn't exposed publicly.
        Vector3 muzzlePosition = user.transform.position + Vector3.up * AIM_HEIGHT_OFFSET;
        Vector3 targetPosition = currentTarget.position + Vector3.up * AIM_HEIGHT_OFFSET;

        bool solved = Sc_ProjectileArcSolver.TrySolveAngle(
            muzzlePosition,
            targetPosition,
            _projectileData.LaunchSpeed,
            out Vector3 fireDirection
        );

        if (!solved)
        {
            // Target is out of range for this projectile's LaunchSpeed — the
            // solver already gave us the best-effort 45-degree shot, but it
            // will land short. Log so this is easy to notice during playtesting.
            Debug.LogWarning($"[Sc_ShovyDirtShot] Target out of range for LaunchSpeed " +
                             $"{_projectileData.LaunchSpeed}. Consider raising LaunchSpeed " +
                             $"on the ProjectileData asset.");
        }

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