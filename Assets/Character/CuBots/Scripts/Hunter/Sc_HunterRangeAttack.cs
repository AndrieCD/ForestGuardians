// Sc_HunterRangeAttack.cs
// Hunter CuBot's ranged attack — fires a single bolt toward the player
// after a short windup pause.
//
// WINDUP FLOW (unchanged from original):
//   1. CheckCooldown() — bail if still on CD
//   2. Start cooldown immediately so Hunter can't chain attacks during windup
//   3. Signal controller to freeze movement (_onWindupStart callback)
//   4. Wait WINDUP_DURATION seconds while animation plays
//   5. Fire bolt via _launcher.FireToward() aimed at cached player position
//   6. Signal controller to resume movement (_onWindupEnd callback)
//
// PROJECTILE SYSTEM CHANGE:
//   Previously used Instantiate() + SetOwner/SetOwnerTag/SetDamageAmount.
//   Now delegates all spawning to _launcher.FireToward() — one call, no setup boilerplate.
//   SO_ProjectileData drives all bolt behavior (speed, range, hit effects).
//
// AIM CHANGE:
//   Previously called GameObject.FindGameObjectWithTag("Player") on every shot
//   inside a coroutine — a scene search every attack is expensive and error-prone.
//   Player Transform is now cached in the constructor and refreshed lazily only
//   if the reference has gone null (e.g. after a scene reload).
//
// POOL SAFETY:
//   Hunter CuBots are reused from a pool — the cached player Transform reference
//   remains valid across pool cycles because the player is never destroyed and
//   re-instantiated mid-stage. If the reference is ever null when Shoot() runs
//   (e.g. player died and the GameObject was deactivated), we fall back to firing
//   straight ahead rather than crashing.

using System.Collections;
using UnityEngine;

public class Sc_HunterRangeAttack : Sc_BaseAbility
{
    // How long Hunter pauses before the bolt fires — tune to match attack animation
    // TODO: Tune WINDUP_DURATION once Hunter's attack animation is finalized.
    //       Current value of 0.5s is a placeholder — typical ranged windup is 0.3–0.6s.
    private const float WINDUP_DURATION = 0.5f;

    // SO_ProjectileData asset for Hunter's bolt.
    // TODO: Assign Hunter_Bolt SO_ProjectileData asset to SO_Ability.ProjectileData
    //       in the Inspector. Hunter_Bolt should have:
    //         LaunchSpeed  = 18        (slightly slower than Rajah's feather for readability)
    //         MaxRange     = 30        (Hunter is a mid-range enemy)
    //         IsPiercing   = false
    //         HitEffects   = [ Sc_HitEffect_Knockback (Force = 8, Duration = 0.2) ]
    private SO_ProjectileData _projectileData;

    // Cached launcher — fetched in constructor from user's GameObject.
    // Mb_ProjectileLauncher must be attached to the Hunter prefab root.
    private Mb_ProjectileLauncher _launcher;

    // Cached player Transform — fetched once and reused every shot.
    // Refreshed lazily in Shoot() if it has gone null.
    private Transform _playerTransform;

    // Callbacks wired by Mb_HunterController — freeze/resume movement around windup
    private readonly System.Action _onWindupStart;
    private readonly System.Action _onWindupEnd;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Sc_HunterRangeAttack(
        SO_Ability abilityData,
        Mb_CharacterBase user,
        System.Action onWindupStart,
        System.Action onWindupEnd)
        : base(abilityData, user)
    {
        _onWindupStart = onWindupStart;
        _onWindupEnd = onWindupEnd;

        _projectileData = abilityData.ProjectileData;

        // Cache the launcher from the CuBot's own GameObject.
        // CuBots don't use OnEquip for component caching the way Guardian abilities do —
        // the constructor runs immediately after the ability is created in AssignAbilities(),
        // at which point the user MonoBehaviour is fully initialized and safe to query.
        _launcher = user.GetComponent<Mb_ProjectileLauncher>();

        if (_launcher == null)
            Debug.LogError("[Sc_HunterRangeAttack] No Mb_ProjectileLauncher found on " +
                           $"{user.gameObject.name}. Add the component to the Hunter prefab.");

        // Cache the player Transform once — avoids a scene search every attack.
        // FindGameObjectWithTag is acceptable here in the constructor (called at spawn
        // time, not per-frame) but not inside Shoot() which runs inside a coroutine.
        CachePlayerTransform();
    }


    // -------------------------------------------------------------------------
    // Ability Lifecycle
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        // Start cooldown before windup so Hunter can't queue another attack
        // while waiting for the bolt to fire. This matches the original behavior.
        StartCooldown(user, GetAttackCooldown(user));

        user.StartCoroutine(WindupAndShoot(user));
    }


    // -------------------------------------------------------------------------
    // Windup Coroutine
    // -------------------------------------------------------------------------

    private IEnumerator WindupAndShoot(Mb_CharacterBase user)
    {
        // Freeze movement — Hunter plants its feet during the windup animation
        _onWindupStart?.Invoke();

        // TODO: Trigger Hunter's attack windup animation here via user's Animator
        //       once Hunter's Animator Controller and trigger hashes are set up.

        yield return new WaitForSeconds(WINDUP_DURATION);

        Shoot(user);

        // Resume movement — Hunter can path again after the bolt leaves
        _onWindupEnd?.Invoke();
    }


    // -------------------------------------------------------------------------
    // Shoot
    // -------------------------------------------------------------------------

    private void Shoot(Mb_CharacterBase user)
    {
        if (_launcher == null)
        {
            Debug.LogError("[Sc_HunterRangeAttack] Cannot fire — Mb_ProjectileLauncher is null.");
            return;
        }

        if (_projectileData == null)
        {
            Debug.LogError("[Sc_HunterRangeAttack] Cannot fire — SO_ProjectileData is null. " +
                           "Assign Hunter_Bolt to SO_Ability.ProjectileData.");
            return;
        }

        // Refresh cached player Transform if it has gone null.
        // This handles the edge case where the player was deactivated and reactivated
        // between Hunter spawns — unlikely mid-stage but safe to guard against.
        if (_playerTransform == null)
            CachePlayerTransform();

        // Resolve the fire direction toward the player's center mass.
        // Adding Vector3.up * 1.0f offsets the target to chest height so bolts
        // don't clip the floor when Hunter and the player are on level ground.
        // If player is still null after refresh (e.g. player is dead), fall back
        // to straight ahead so the coroutine completes cleanly without crashing.
        Vector3 targetPoint = _playerTransform != null
            ? _playerTransform.position + Vector3.up * 1.0f
            : user.transform.position + user.transform.forward * 10f;

        // Fire origin: Hunter's own position at chest height.
        // We don't use ProjectileOrigin here because Hunter doesn't have a dedicated
        // hand/barrel transform yet — the launcher's LaunchOrigin handles positioning.
        // TODO: Add a LaunchOrigin child Transform to the Hunter prefab (at chest height)
        //       and assign it to Mb_ProjectileLauncher.LaunchOrigin in the Inspector.
        //       This removes the need for the Vector3.up offset on targetPoint above.
        Vector3 fireDirection = (targetPoint - user.transform.position).normalized;

        // Calculate damage at fire time — crit rolled here, passed into FireToward().
        // Hunter's bolt can crit; the final damage is locked in at fire time.
        float damage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue()
        );
        damage = ApplyCriticalStrike(damage, user);

        // FireToward() spawns, orients, and initializes the projectile in one call.
        // The launcher detects that user is a CuBot (no Mb_GuardianBase component)
        // and would normally fall back to NavMeshAgent facing — but we pass an explicit
        // direction here via FireToward() so aim resolution is bypassed entirely.
        // This is correct: Hunter always aims at the player, not its NavMesh heading.
        Mb_Projectile projectile = _launcher.FireToward(
            _projectileData,
            user,
            damage,
            fireDirection
        );

        // Hunter bolts do not trigger Guardian passives — no OnHit subscription needed.
        // If a future Hunter variant needs on-hit callbacks (e.g. a poison on-hit),
        // subscribe here:
        //   if (projectile != null)
        //       projectile.OnHit += (target, hitPoint, hitNormal) => { ... };
    }


    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Finds and caches the player's Transform by tag.
    // Called once in the constructor and lazily in Shoot() if the reference goes null.
    private void CachePlayerTransform()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("[Sc_HunterRangeAttack] No GameObject with tag 'Player' found. " +
                             "Hunter will fire straight ahead until the player is found.");
        }
    }
}