// Rajah_R_Branch2.cs
// [R] Eagle Eye — Rajah's second ultimate branch.
//
// PASSIVE: Every Secondary shot fires 1–4 additional feathers after a short delay.
//   Extra shot count scales with Guardian level (4 → 1, 8 → 2, 12 → 4).
//   Listens to Rajah_Secondary.OnSecondaryFired — receives launch origin and
//   normalized direction so FireToward() can be called directly without re-aiming.
//
// ACTIVE: Locks abilities/attacks and fires a continuous stream of spread feathers
//   for ACTIVE_DURATION seconds. Each shot is offset randomly within a circle
//   around the ProjectileOrigin before direction is recalculated, giving the
//   burst its characteristic spray pattern.
//
// PROJECTILE SYSTEM CHANGE:
//   Previously used Instantiate() + SetOwner/SetOwnerTag/SetDamageAmount per shot.
//   Now delegates all spawning to _launcher.FireToward() — one call per projectile.
//   SpawnProjectile() now takes a normalized direction instead of a world targetPoint,
//   matching the updated OnSecondaryFired signature from Rajah_Secondary.

using System.Collections;
using UnityEngine;

public class Rajah_R_Branch2 : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Passive Config
    // -------------------------------------------------------------------------

    private const float EXTRA_SHOT_DELAY = 0.2f;

    // Radius of the random spawn offset circle for passive extra shots.
    // Smaller than active — passive shots should feel tighter and more controlled.
    private const float PASSIVE_SPREAD_RADIUS = 0.5f;

    // Handle to the running extra-shot coroutine so we can cancel a previous
    // burst if another Secondary fires before the last one finishes.
    private Coroutine _passiveRoutine;

    private Mb_HealthComponent _health;

    // -------------------------------------------------------------------------
    // Active Config
    // -------------------------------------------------------------------------

    private const float ACTIVE_DURATION = 4f;
    private const float FIRE_INTERVAL = 0.1f;       // 

    // Radius of the random spawn offset circle for active burst shots.
    // Larger than passive — active mode is intentionally chaotic and wide.
    private const float ACTIVE_SPREAD_RADIUS = 1f;

    // SO_ProjectileData asset for Eagle Eye's feather shots.
    // TODO: Add a ProjectileData field to SO_Ability and assign Rajah_Feather_Basic here:
    //   _projectileData = abilityData.ProjectileData;
    private SO_ProjectileData _projectileData;
    private GameObject _projectilePrefab;

    // Cached launcher — fetched once in OnEquip from the owner's GameObject
    private Mb_ProjectileLauncher _launcher;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Rajah_R_Branch2(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
        _projectileData = abilityData.ProjectileData;
    }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        _launcher = user.GetComponent<Mb_ProjectileLauncher>();
        _health = user.GetComponent<Mb_HealthComponent>();

        // Fetch the generic projectile prefab from the registry
        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Rajah_FeatherProjectile);

        if (_launcher == null)
            Debug.LogError("[Rajah_R_Branch2] No Mb_ProjectileLauncher found on " +
                           $"{user.gameObject.name}. Add the component to the Guardian prefab.");

        // Subscribe to Secondary fired event so passive extra shots can trigger.
        // Unsubscribe-first pattern prevents duplicate listeners if OnEquip is
        // somehow called twice (e.g. hot-reload or re-equip during development).
        Rajah_Secondary.OnSecondaryFired -= HandleSecondaryFired;
        Rajah_Secondary.OnSecondaryFired += HandleSecondaryFired;
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Rajah_Secondary.OnSecondaryFired -= HandleSecondaryFired;

        // Cancel any in-flight passive burst so deactivation is clean.
        // Without this, a burst coroutine could still be running after the
        // ability is unequipped — e.g. if the guardian died mid-burst.
        if (_passiveRoutine != null)
        {
            _User.StopCoroutine(_passiveRoutine);
            _passiveRoutine = null;
        }
    }


    // -------------------------------------------------------------------------
    // Passive — Extra Shots on Secondary Fire
    // -------------------------------------------------------------------------

    // Receives the origin and normalized direction from Rajah_Secondary.OnSecondaryFired.
    // Filters by source so Eagle Eye only reacts to this guardian's own Secondary shots,
    // not shots from a second guardian if multiplayer is ever added.
    private void HandleSecondaryFired(Mb_CharacterBase source, Vector3 origin, Vector3 direction)
    {
        if (source != _User) return;

        int extraShots = GetExtraShotCount();
        if (extraShots == 0) return;

        // Cancel a previous burst if the player fires quickly — prevents two bursts
        // from stacking and firing more shots than intended.
        if (_passiveRoutine != null)
            _User.StopCoroutine(_passiveRoutine);

        _passiveRoutine = _User.StartCoroutine(
            FireExtraShots(origin, direction, extraShots)
        );
    }


    // Fires each extra shot after a short delay so they trail behind the initial shot.
    // Uses the same origin and direction as the triggering Secondary — extra shots
    // fly parallel to the original but with a random spread offset applied in SpawnProjectile.
    private IEnumerator FireExtraShots(Vector3 origin, Vector3 direction, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(EXTRA_SHOT_DELAY);
            SpawnProjectile(origin, direction, isPassive: true);
        }

        _passiveRoutine = null;
    }


    // Returns how many extra shots fire per Secondary based on guardian level.
    // Thresholds: level 3 → 1 shot, level 8 → 2 shots, level 12 → 3 shots.
    private int GetExtraShotCount()
    {
        int level = _User.GetLevel();

        if (level >= 12) return 3;
        if (level >= 8) return 2;
        if (level >= 1) return 1;
        return 0;
    }


    // -------------------------------------------------------------------------
    // Active — Continuous Spread Burst
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        TriggerAbilityAnimation(user);

        // Lock abilities and attacks for the duration so the player can't
        // interrupt or layer the burst with other skills.
        var controller = user as Mb_PlayerController;
        controller?.AddDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );

        user.StartCoroutine(ActiveRoutine(user, controller));

        StartCooldown(user, GetAbilityCooldown(user));

    }


    private IEnumerator ActiveRoutine(Mb_CharacterBase user, Mb_PlayerController controller)
    {
        SetUntargetable(true);


        float elapsed = 0f;

        while (elapsed < ACTIVE_DURATION)
        {
            FireSpreadShot(user);
            yield return new WaitForSeconds(FIRE_INTERVAL);
            elapsed += FIRE_INTERVAL;
        }

        controller?.RemoveDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );

        SetUntargetable(false);

        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.EndR2Ability();
    }


    // Resolves the current aim point from the camera for each active burst shot,
    // then delegates to SpawnProjectile with a direction computed from origin → aim.
    // We resolve aim locally here rather than using _launcher.Fire() because this
    // fires inside a coroutine loop — each shot needs a fresh aim direction to
    // track where the player is pointing as the burst plays out.
    private void FireSpreadShot(Mb_CharacterBase user)
    {
        if (_Guardian.ProjectileOrigin == null) return;

        Camera cam = Camera.main;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        int aimMask = (1 << LayerMask.NameToLayer("Default")) |
                      (1 << LayerMask.NameToLayer("Character"));

        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 1000f, aimMask)
            ? hit.point
            : ray.origin + ray.direction * 100f;

        // Guard: if the aim target is behind the ProjectileOrigin, fall back to
        // Guardian body forward so the burst doesn't fire backward into the player.
        Vector3 toTarget = targetPoint - _Guardian.ProjectileOrigin.position;
        Vector3 direction = Vector3.Dot(_Guardian.transform.forward, toTarget) > 0f
            ? toTarget.normalized
            : _Guardian.transform.forward;

        SpawnProjectile(_Guardian.ProjectileOrigin.position, direction, isPassive: false);
    }


    // -------------------------------------------------------------------------
    // Projectile Spawning — shared by passive and active paths
    // -------------------------------------------------------------------------

    // Spawns one Eagle Eye feather shot using FireToward().
    // A random circular offset is applied to the spawn origin before direction
    // is recalculated — this is what gives Eagle Eye its spray pattern.
    // Passive shots use a tighter radius; active burst shots use a wider one.
    private void SpawnProjectile(Vector3 origin, Vector3 baseDirection, bool isPassive)
    {
        if (_launcher == null) return;
        if (_projectileData == null) return;

        // Select spread radius based on whether this is a passive or active shot.
        // Active shots are intentionally wider — the burst feel depends on this.
        float spreadRadius = isPassive ? PASSIVE_SPREAD_RADIUS : ACTIVE_SPREAD_RADIUS;

        // Random offset within a circle in the Guardian's local right/up plane.
        // Applied to the origin so the direction automatically fans outward after
        // the recalculation below — the further from center, the more the shot angles out.
        Vector2 randomCircle = Random.insideUnitCircle * spreadRadius;
        Vector3 offset = (_Guardian.transform.right * randomCircle.x)
                       + (_Guardian.transform.up * randomCircle.y);

        Vector3 spawnPosition = origin + offset;

        // Recalculate direction from the offset spawn position toward the original
        // aim target (reconstructed from baseDirection + a large distance).
        // We reconstruct the target point by projecting baseDirection far forward
        // from the original (pre-offset) origin, then re-deriving direction from
        // the new spawn position — this preserves the intended aim point while
        // introducing natural angular spread from the offset.
        Vector3 targetPoint = origin + baseDirection * 100f;
        Vector3 finalDirection = (targetPoint - spawnPosition).normalized;


        // Calculate damage at fire time using current ability level and ATK.
        // Crit is rolled per shot — each Eagle Eye feather can independently crit.
        float damage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            _User.Stats.AttackPower.GetValue()
        );
        damage = ApplyCriticalStrike(damage, _User);

        // Check if passive or not, passive additional feathers are at 75% efficiency

        if (isPassive) {
            damage *= 0.75f;
        }

        // FireToward() handles: spawn at spawnPosition, orient toward finalDirection,
        // call Initialize(data, owner, damage), then SetActive(true).
        // We override the launcher's LaunchOrigin by passing spawnPosition indirectly —
        // FireToward() uses LaunchOrigin from the component, not an argument.
        // TODO: If per-shot spawn position override becomes necessary, add an overload:
        //   FireToward(data, owner, damage, direction, Vector3 overrideOrigin)
        //   For now, the spread offset is small enough that LaunchOrigin is close enough.
        Mb_Projectile projectile = _launcher.FireToward(_projectilePrefab,
            _projectileData,
            _User,
            damage,
            spawnPosition,
            finalDirection
        );

        if (projectile == null) return;

        // Spawn VFX at projectile location.
        Mb_VFXManager.Play(VFXType.Rajah_R_Branch2_Cast, projectile.gameObject.transform.position, projectile.transform.rotation);


        // Eagle Eye passive shots intentionally do not trigger Royal Plumage stacks —
        // only the original Secondary shot (which has its own OnHit subscription in
        // Rajah_Secondary) grants stacks. Extra shots are bonus damage only.
        // Active burst shots also do not grant stacks — the burst would trivially
        // max out the passive stack count in one activation otherwise.

        Mb_AudioManager.PlaySFX(CombatSFX.Rajah_Feather_Launch);
    }

    private void SetUntargetable(bool state)
    {
        if (_health == null) return;

        // ✅ REQUIRED IMPLEMENTATION IN Mb_HealthComponent:
        // public bool IsUntargetable;
        _health.IsUntargetable = state;
    }


    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerR2Ability();
    }
}