// Rajah_E_Ability.cs
// [E] Feather Barrage — Rajah leaps backward, then fires a spread of feathers forward.
// Leap direction is opposite the camera. Feathers fan out in a horizontal cone.
// Scales with ATK and AP at current ability level.
//
// PROJECTILE SYSTEM CHANGE:
//   Previously used Instantiate() inside a loop with manual setup calls per projectile.
//   Now calls _launcher.FireToward() per feather — each with a pre-calculated spread
//   direction. Mb_ProjectileLauncher handles all spawn, position, orient, initialize logic.
//
// SIBLING COLLISION:
//   Physics.IgnoreCollision() is preserved — the five feathers in the spread must not
//   hit each other mid-flight. Because FireToward() returns each Mb_Projectile instance,
//   we collect their Colliders after firing and ignore-pair them the same way as before.
//
// AIM RESOLUTION:
//   GetAimTarget() is kept as a local helper because E needs the world-space aim point
//   to calculate each feather's rotated direction before passing it to FireToward().
//   The launcher's internal aim resolution is bypassed here by design — spread shots
//   must fan around a single shared center, not re-resolve aim independently per shot.

using UnityEngine;

public class Rajah_E_Ability : Sc_BaseAbility
{
    private const float LEAP_SPEED = 25f;
    private const float LEAP_DURATION = 0.25f;
    private const int FEATHER_COUNT = 5;
    private const float SPREAD_ANGLE = 30f; // Total cone width — 15° left and right of center

    // SO_ProjectileData asset for the E spread feather.
    // TODO: Add a ProjectileData field to SO_Ability and assign Rajah_Feather_Spread here:
    //   _projectileData = abilityData.ProjectileData;
    private SO_ProjectileData _projectileData;

    // Cached launcher — fetched once in OnEquip
    private Mb_ProjectileLauncher _launcher;


    public Rajah_E_Ability(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
        _projectileData = abilityData.ProjectileData;
    }


    public override void OnEquip(Mb_CharacterBase user)
    {
        _launcher = user.GetComponent<Mb_ProjectileLauncher>();

        if (_launcher == null)
            Debug.LogError("[Rajah_E_Ability] No Mb_ProjectileLauncher found on " +
                           $"{user.gameObject.name}. Add the component to the Guardian prefab.");

        Debug.Log($"Equipped {_AbilityData.AbilityName}.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"Unequipped {_AbilityData.AbilityName}.");
    }


    // Called when the player presses [E]
    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        LeapBackward(user);
        FireFeatherSpread(user);
        TriggerAbilityAnimation(user);

        Mb_AudioManager.PlaySFX(CombatSFX.Rajah_E_Launch, user.gameObject.transform.position);

        // E is an ability — cooldown reduced by Haste, not AttackSpeed
        StartCooldown(user, GetAbilityCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Leap
    // -------------------------------------------------------------------------

    // Launches Rajah in the direction opposite the camera, with a small upward kick.
    // No changes from the original — leap logic is independent of the projectile system.
    private void LeapBackward(Mb_CharacterBase user)
    {
        Camera cam = Camera.main;

        Vector3 leapDir = -cam.transform.forward;
        leapDir.y = 0f;
        leapDir.Normalize();

        // Fallback if camera is pointing straight up or down
        if (leapDir == Vector3.zero)
            leapDir = -user.transform.forward;

        // Small upward kick so the leap feels like a jump rather than a ground slide
        leapDir += Vector3.up * 0.4f;
        leapDir.Normalize();

        user.Movement.StartDash(leapDir * LEAP_SPEED, LEAP_DURATION);
    }


    // -------------------------------------------------------------------------
    // Spread Fire
    // -------------------------------------------------------------------------

    // Fires FEATHER_COUNT projectiles in a horizontal fan toward the aim target.
    // Each feather is fired via FireToward() with a pre-calculated direction.
    // After all shots are fired, sibling colliders are paired with IgnoreCollision
    // so the spread feathers cannot hit each other mid-flight.
    private void FireFeatherSpread(Mb_CharacterBase user)
    {
        if (_launcher == null)
        {
            Debug.LogError("[Rajah_E_Ability] Cannot fire — Mb_ProjectileLauncher is null.");
            return;
        }

        if (_projectileData == null)
        {
            Debug.LogError("[Rajah_E_Ability] Cannot fire — SO_ProjectileData is null. " +
                           "Assign Rajah_Feather_Spread to SO_Ability.ProjectileData.");
            return;
        }

        // Resolve the shared aim point once — all feathers fan around this center.
        // We keep this local raycast here rather than relying on the launcher's internal
        // aim resolution because FireToward() bypasses aim resolution by design.
        Vector3 aimTarget = GetAimTarget();
        Vector3 centerDir = (aimTarget - _Guardian.ProjectileOrigin.position).normalized;

        // Damage is the same for every feather in the spread — calculate once.
        // E scales with both ATK and AP, unlike Secondary which is ATK-only.
        // No crit roll on E — spread shots intentionally don't crit individually.
        // TODO: Revisit crit behavior for E if ability design changes.
        float damagePerFeather = _AbilityData.GetStat(
            "Damage", CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );

        // Collect fired projectiles so we can ignore-pair their colliders below.
        Mb_Projectile[] firedProjectiles = new Mb_Projectile[FEATHER_COUNT];

        for (int i = 0; i < FEATHER_COUNT; i++)
        {
            // Map feather index to a normalized 0–1 position across the spread.
            // t = 0 → leftmost feather, t = 1 → rightmost feather.
            float t = (FEATHER_COUNT == 1) ? 0f : (float)i / (FEATHER_COUNT - 1);

            // Rotate centerDir horizontally by the interpolated angle offset.
            float angleOffset = Mathf.Lerp(-SPREAD_ANGLE / 2f, SPREAD_ANGLE / 2f, t);
            Vector3 featherDir = Quaternion.AngleAxis(angleOffset, Vector3.up) * centerDir;

            // FireToward() spawns, positions, orients, and initializes the projectile.
            // It returns the instance so we can collect it for the ignore-collision pass.
            firedProjectiles[i] = _launcher.FireToward(
                _projectileData,
                user,
                damagePerFeather,
                featherDir
            );

            Mb_AudioManager.PlaySFX(CombatSFX.Rajah_Feather_Launch);
        }

        // Prevent sibling feathers from hitting each other mid-flight.
        // We iterate every unique pair (i, j where j > i) and tell the physics
        // engine to ignore collisions between them for their entire flight.
        // This is the same approach as the original — preserved because it's correct.
        for (int i = 0; i < firedProjectiles.Length; i++)
        {
            for (int j = i + 1; j < firedProjectiles.Length; j++)
            {
                // Either projectile may be null if FireToward() failed — guard before use
                if (firedProjectiles[i] == null || firedProjectiles[j] == null) continue;

                Collider a = firedProjectiles[i].GetComponent<Collider>();
                Collider b = firedProjectiles[j].GetComponent<Collider>();

                if (a != null && b != null)
                    Physics.IgnoreCollision(a, b);
            }
        }
    }


    // -------------------------------------------------------------------------
    // Aim Helper
    // -------------------------------------------------------------------------

    // Raycasts from screen center to find the world-space point the player is aiming at.
    // Used to calculate the center direction that the spread fans around.
    // Kept local rather than delegating to the launcher because E needs the raw
    // world point to rotate each feather direction around — not a single fire direction.
    private Vector3 GetAimTarget()
    {
        Camera cam = Camera.main;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        return Physics.Raycast(ray, out RaycastHit hit, 1000f)
            ? hit.point
            : ray.origin + ray.direction * 100f;
    }


    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerEAbility();
    }
}