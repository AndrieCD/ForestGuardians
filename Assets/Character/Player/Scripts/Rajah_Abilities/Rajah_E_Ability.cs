// Rajah_E_Ability.cs
// [E] Feather Barrage — Rajah leaps backward, then fires a spread of feathers forward.
// Leap direction is opposite the camera. Feathers fan out in a horizontal cone.
// Scales with ATK and AP at current ability level.

using UnityEngine;

public class Rajah_E_Ability : Sc_BaseAbility
{
    private const float LEAP_SPEED = 25f;
    private const float LEAP_DURATION = 0.25f;
    private const int FEATHER_COUNT = 5;
    private const float SPREAD_ANGLE = 30f; // Total cone width — 15° left and right of center

    private Camera _cam;

    // Cached once in OnEquip — avoids a scene search every activation
    private Transform _projectileOrigin;


    public Rajah_E_Ability(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
        _cam = Camera.main;
    }


    public override void OnEquip(Mb_CharacterBase user)
    {
        // Cache the spawn point once — E fires multiple projectiles per use
        // so avoiding repeated Find() calls matters more here than in Secondary
        GameObject originObj = GameObject.Find("ProjectileOrigin");
        if (originObj != null)
            _projectileOrigin = originObj.transform;
        else
            Debug.LogError("[Rajah_E_Ability] 'ProjectileOrigin' GameObject not found in scene.");

        Debug.Log($"[{user.name}] Equipped {_AbilityData.AbilityName}.");
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Unequipped {_AbilityData.AbilityName}.");
    }


    // Called when the player presses [E]
    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        LeapBackward(user);
        FireFeatherSpread(user);
        TriggerAbilityAnimation(user);

        // E is an ability — cooldown is reduced by Haste, not AttackSpeed
        StartCooldown(user, GetAbilityCooldown(user));
    }


    // Launches Rajah in the direction opposite the camera, with a small upward kick
    private void LeapBackward(Mb_CharacterBase user)
    {
        Vector3 leapDir = -_cam.transform.forward;
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


    // Spawns FEATHER_COUNT projectiles in a horizontal fan toward the aim target
    private void FireFeatherSpread(Mb_CharacterBase user)
    {
        GameObject prefab = _AbilityData.ProjectileModel;

        if (prefab == null)
        {
            Debug.LogWarning("[Rajah_E_Ability] No ProjectileModel assigned on SO_Ability.");
            return;
        }

        if (_projectileOrigin == null)
        {
            Debug.LogError("[Rajah_E_Ability] ProjectileOrigin is not cached.");
            return;
        }

        Vector3 aimTarget = GetAimTarget();
        Vector3 centerDir = (aimTarget - _projectileOrigin.position).normalized;
        float damagePerFeather = _AbilityData.GetStat(
            "Damage", CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );

        // Store spawned instances so we can tell them to ignore each other's colliders
        GameObject[] spawnedFeathers = new GameObject[FEATHER_COUNT];

        for (int i = 0; i < FEATHER_COUNT; i++)
        {
            // Map feather index to a normalized 0–1 position across the spread
            float t = (FEATHER_COUNT == 1) ? 0f : (float)i / (FEATHER_COUNT - 1);

            // Interpolate the angle offset from left edge to right edge of the cone
            float angleOffset = Mathf.Lerp(-SPREAD_ANGLE / 2f, SPREAD_ANGLE / 2f, t);
            Vector3 featherDir = Quaternion.AngleAxis(angleOffset, Vector3.up) * centerDir;

            spawnedFeathers[i] = GameObject.Instantiate(
                prefab,
                _projectileOrigin.position,
                Quaternion.LookRotation(featherDir)
            );

            Mb_Projectile projectile = spawnedFeathers[i].GetComponent<Mb_Projectile>();
            if (projectile != null)
                projectile.SetDamageAmount(damagePerFeather);
        }

        // Prevent sibling feathers from colliding with each other mid-flight
        for (int i = 0; i < spawnedFeathers.Length; i++)
        {
            for (int j = i + 1; j < spawnedFeathers.Length; j++)
            {
                Collider a = spawnedFeathers[i]?.GetComponent<Collider>();
                Collider b = spawnedFeathers[j]?.GetComponent<Collider>();
                if (a != null && b != null)
                    Physics.IgnoreCollision(a, b);
            }
        }
    }


    // Raycasts from screen center to find where the player is aiming
    private Vector3 GetAimTarget()
    {
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        return Physics.Raycast(ray, out RaycastHit hit, 1000f)
            ? hit.point
            : ray.origin + ray.direction * 100f;
    }


    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerEAbility();
    }
}