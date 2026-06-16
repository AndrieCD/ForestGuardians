// Mari_Primary.cs
// [LMB] Mind Flurry — Mari's primary attack.
//
// BEHAVIOR:
//   Each click fires a burst of BURST_COUNT (5) projectiles in rapid succession,
//   with BURST_INTERVAL (0.08s) between each shot. All projectiles fly toward
//   the same resolved aim point (camera center raycast), calculated once at the
//   start of the burst so the spread stays coherent even if Mari moves mid-burst.
//
// COOLDOWN:
//   Uses AttackSpeed via GetAttackCooldown() — same as Rajah's primary/secondary.
//   The cooldown starts AFTER the full burst fires so rapid clicks don't stack
//   multiple overlapping bursts.
//
// PASSIVE INTERACTION:
//   RaiseBasicAttackHit() is subscribed to each projectile's OnHit event —
//   only confirmed hits grant Mental Overflow stacks, not missed shots.
//   Up to 5 stacks can be granted in a single burst if all shots connect.
//
// INPUT BLOCKING:
//   AllAttacks is disabled for the burst duration (~0.32s) to prevent the
//   player double-clicking and launching two overlapping bursts.
//   Removed automatically when the burst coroutine ends.
//
// VFX:
//   "MindFlurryMuzzleVFX" — a child ParticleSystem on Mari's prefab.
//   Plays once per burst (not per projectile). Located by name in OnEquip.
//   TODO: Leo — add a short burst particle system named "MindFlurryMuzzleVFX"
//         at Mari's ProjectileOrigin child.

using System;
using System.Collections;
using UnityEngine;

public class Mari_Primary : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Config
    // -------------------------------------------------------------------------

    // Number of projectiles per LMB click
    // TODO: Tune — 5 is the spec value
    private const int BURST_COUNT = 5;

    // Delay between each projectile in the burst (seconds)
    // TODO: Tune — 0.08s matches spec, gives ~0.32s total burst window
    private const float BURST_INTERVAL = 0.08f;


    // -------------------------------------------------------------------------
    // Cached References
    // -------------------------------------------------------------------------

    private Mb_ProjectileLauncher _launcher;
    private SO_ProjectileData _projectileData;
    private GameObject _projectilePrefab;

    // Muzzle flash VFX — located by name in OnEquip
    private ParticleSystem _muzzleVFX;


    public static event Action<float, Mb_CharacterBase, MB_CuBotBase> OnPrimaryHit;



    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Mari_Primary(SO_Ability abilityData, Mb_CharacterBase user)
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

        if (_launcher == null)
            Debug.LogError("[Mari_Primary] No Mb_ProjectileLauncher found on " +
                           $"{user.gameObject.name}.");

        // Fetch the generic projectile prefab from the registry
        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Mari_PsychicLeafProjectile);

        // Locate muzzle VFX by name in the prefab hierarchy
        foreach (ParticleSystem ps in user.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "MindFlurryMuzzleVFX")
            {
                _muzzleVFX = ps;
                break;
            }
        }

        Debug.Log($"[Mari_Primary] Equipped {_AbilityData.AbilityName}.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[Mari_Primary] Unequipped {_AbilityData.AbilityName}.");
    }


    // -------------------------------------------------------------------------
    // Activation
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        // Block further attack input for the burst window so the player
        // cannot stack multiple overlapping bursts from rapid clicks
        var controller = user as Mb_PlayerController;
        controller?.AddDisable(ActionDisableFlags.AllAttacks);

        // Play muzzle flash once for the whole burst
        _muzzleVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _muzzleVFX?.Play();

        TriggerAbilityAnimation(user);

        user.StartCoroutine(BurstRoutine(user, controller));

        // Cooldown starts now — prevents the player firing before the burst ends
        // on high AttackSpeed builds where cooldown < burst duration
        StartCooldown(user, GetAttackCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Burst Coroutine
    // -------------------------------------------------------------------------

    // Fires BURST_COUNT projectiles with BURST_INTERVAL delay between each.
    // Aim point is resolved ONCE before the loop so all shots share the same
    // direction — keeps the burst coherent even if the camera moves mid-burst.
    private IEnumerator BurstRoutine(Mb_CharacterBase user, Mb_PlayerController controller)
    {

        for (int i = 0; i < BURST_COUNT; i++)
        {
            // Resolve aim direction once for the whole burst
            Vector3 aimTarget = GetAimTarget();

            FireSingleProjectile(user, aimTarget);

            // Don't wait after the last shot
            if (i < BURST_COUNT - 1)
                yield return new WaitForSeconds(BURST_INTERVAL);
        }

        // Re-enable attacks now that the burst is finished
        controller?.RemoveDisable(ActionDisableFlags.AllAttacks);
    }


    // -------------------------------------------------------------------------
    // Single Shot
    // -------------------------------------------------------------------------

    private void FireSingleProjectile(Mb_CharacterBase user, Vector3 aimTarget)
    {
        if (_launcher == null)
        {
            Debug.LogError("[Mari_Primary] Cannot fire — launcher is null.");
            return;
        }

        if (_projectileData == null)
        {
            Debug.LogError("[Mari_Primary] Cannot fire — ProjectileData is null. " +
                           "Assign Mari_PsychicLeaf to SO_Ability.ProjectileData.");
            return;
        }

        // Calculate damage at fire time with current level and AP scaling
        // Mind Flurry scales with AbilityPower — it's a psychic barrage, not a physical one
        float damage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );

        // No individual crit per leaf — the burst as a whole can crit
        // TODO: Revisit — could allow per-leaf crits once damage numbers are tuned

        // Compute fire direction from ProjectileOrigin to resolved aim target
        if (_Guardian == null || _Guardian.ProjectileOrigin == null)
        {
            Debug.LogError("[Mari_Primary] ProjectileOrigin is not set on Mari's prefab.");
            return;
        }
        // Add offsets to spawn origin, but direction remains toward the aim target — gives a slight spread without needing to randomize fire direction
        var spawnOffset = new Vector3(
            UnityEngine.Random.Range(-1f, 1f), // X offset
            UnityEngine.Random.Range(-1f, 1f), // Y offset
            0f // No Z offset to keep projectiles from spawning too far forward/backward
        );

        var spawnPosition = _Guardian.ProjectileOrigin.position + spawnOffset;

        Vector3 fireDir =
            (aimTarget - spawnPosition).normalized;

        Mb_Projectile projectile = _launcher.FireToward(_projectilePrefab,
            _projectileData,
            user,
            damage,
            spawnPosition,
            fireDir
        );

        if (projectile == null) return;

        // Subscribe to OnHit — passive stacks only on confirmed hit, not on fire
        // Lambda captures nothing expensive — safe for pool-reused projectiles
        projectile.OnHit += (target, hitPoint, hitNormal) =>
        {
            Passive_Ability.RaiseBasicAttackHit();

            // Notify Branch1 passive — passes the damage this projectile carried
            // and the source character so Branch1 can filter by owner
            OnPrimaryHit?.Invoke(damage, user, target as MB_CuBotBase);
        };
    }


    // -------------------------------------------------------------------------
    // Aim Helper
    // -------------------------------------------------------------------------

    // Resolves the world-space aim point from the center of the screen.
    // Called once per burst so all projectiles share the same target.
    private Vector3 GetAimTarget()
    {
        Camera cam = Camera.main;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Exclude the Character layer so the ray doesn't hit Mari herself
        int layerMask = ~(1 << LayerMask.NameToLayer("Character"));

        return Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask)
            ? hit.point
            : ray.origin + ray.direction * 100f;
    }


    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        // TODO: Mb_GuardianAnimator needs a TriggerPrimaryAttack() call.
        // Mari's Primary animation should loop or blend differently from Rajah's
        // slash — consider a dedicated "MindFlurry" trigger parameter.
        // For now, reuse the Primary trigger so animation doesn't silently fail.
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerPrimaryAttack();
    }
}