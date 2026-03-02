using UnityEngine;

/// <summary>
/// [E] Feather Barrage: Rajah leaps backward, then fires 5 feathers in a spread forward.
/// </summary>
public class Rajah_E_Ability : Sc_BaseAbility
{
    private Camera _cam;

    private float _leapSpeed = 25f;
    private float _leapDuration = 0.25f;
    private int _featherCount = 5;
    private float _spreadAngle = 30f; // Total cone width/angle, 15° left and right of center

    public Rajah_E_Ability(SO_Ability abilityObject, Mb_CharacterBase user)
        : base(abilityObject, user)
    {
        _cam = Camera.main;
        _Cooldown = _AbilityData.Cooldown;
    }

    public override void OnEquip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} equipped {_AbilityData.AbilityName}.");
    }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown( )) return;

        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerSecondaryAttack( );

        LeapBackward(user);
        FireFeatherSpread(user);
        StartCooldown(user);
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} unequipped {_AbilityData.AbilityName}.");
    }

    // -------------------------------------------------------

    /// <summary>
    /// Calculates a leap direction opposite the camera's forward vector, applies a small upward kick, and tells the character's movement system to dash in that direction for a short duration.
    /// </summary>
    /// <param name="user"></param>
    private void LeapBackward(Mb_CharacterBase user)
    {
        // Get leap direction opposite the camera's forward vector, ignoring vertical component so we leap along the ground plane. 
        Vector3 leapDirection = -_cam.transform.forward;
        leapDirection.y = 0f;
        leapDirection.Normalize( );

        // Safety fallback in case camera is pointing straight up/down
        if (leapDirection == Vector3.zero)
            leapDirection = -user.transform.forward;

        // Add a small upward kick so the leap feels like a jump, not a slide
        leapDirection += Vector3.up * 0.4f;
        leapDirection.Normalize( );

        user.Movement.StartDash(leapDirection * _leapSpeed, _leapDuration);
    }

    /// <summary>
    /// Spawns 5 feather projectiles in a fan shape, all flying toward where the player is aiming.
    /// </summary>
    /// <param name="user"></param>
    private void FireFeatherSpread(Mb_CharacterBase user)
    {
        GameObject projectilePrefab = _AbilityData.ProjectileModel;

        if (projectilePrefab == null)
        {
            Debug.LogWarning("Rajah_E_Ability: No ProjectileModel assigned on the SO_Ability.");
            return;
        }

        // Where the projectiles spawn from
        Transform origin = GameObject.Find("ProjectileOrigin").transform;

        // Figure out where the player is aiming by raycasting from screen center
        Vector3 aimTarget = GetAimTarget(origin.position);

        // The main direction all feathers radiate out from
        Vector3 centerDirection = ( aimTarget - origin.position ).normalized;

        // Calculate damage once — all feathers deal the same amount for simplicity
        float damagePerFeather = _AbilityData.GetStat("Damage", _currentAbilityLevel, user.AttackPower.Value( ), user.AbilityPower.Value( ));

        // Spawn each feather and tell them to ignore each other so they don't self-collide
        GameObject[] spawnedFeathers = new GameObject[_featherCount];

        for (int i = 0; i < _featherCount; i++)
        {
            // Calculate this feather's horizontal angle offset within the cone
            float normalizedPosition;
            if (_featherCount == 1)
            {
                normalizedPosition = 0f;
            } else
            {
                // Convert the feather index (i) into a value between 0 and 1.
                normalizedPosition = (float)i / ( _featherCount - 1 );  // index divided by max index gives us a normalized position along the spread (0 to 1)
            }

            // Get the angle for left and right edges of the cone
            float leftEdgeAngle = -_spreadAngle / 2f;
            float rightEdgeAngle = _spreadAngle / 2f;

            // Interpolate between the left and right edge angles based on the normalized position to get this feather's angle offset
            float angleOffset = Mathf.Lerp(leftEdgeAngle, rightEdgeAngle, normalizedPosition);

            // Rotate the center direction left/right by the offset
            Vector3 featherDirection = Quaternion.AngleAxis(angleOffset, Vector3.up) * centerDirection;
            Quaternion featherRotation = Quaternion.LookRotation(featherDirection);

            spawnedFeathers[i] = GameObject.Instantiate(projectilePrefab, origin.position, featherRotation);

            Mb_Projectile projectile = spawnedFeathers[i].GetComponent<Mb_Projectile>( );
            if (projectile != null)
                projectile.SetDamageAmount(damagePerFeather);
        }

    }

    /// <summary>
    /// Casts a ray from the center of the screen to find where the player is aiming. If it hits something, return that point. If it hits nothing, return a point far away in the direction of the ray.
    /// </summary>
    /// <param name="fallbackOrigin"></param> The point from which to cast the ray if we need a fallback direction (e.g. if the camera is looking at the sky and hits nothing, we still want to shoot forward instead of crashing or doing nothing)
    /// <returns></returns> The world point the player is aiming at
    private Vector3 GetAimTarget(Vector3 fallbackOrigin)
    {
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            return hit.point;

        // If the ray hits nothing, aim far into the distance along the ray
        return ray.origin + ray.direction * 100f;
    }

}