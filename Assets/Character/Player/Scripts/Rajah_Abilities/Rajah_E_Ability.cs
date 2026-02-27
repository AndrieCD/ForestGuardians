using System.Collections.Generic;
using UnityEngine;

public class Rajah_E_Ability : Sc_BaseAbility
{
    private Camera _cam;

    [Header("Leap Settings")]
    public float leapSpeed = 20f;
    public float leapDuration = 0.15f;

    [Header("Feather Spread Settings")]
    public GameObject projectilePrefab;
    public int featherCount = 5;
    public float spreadAngle = 30f;     // Total cone width in degrees. 30f = ±15° from center

    public Rajah_E_Ability(SO_Ability abilityObject, Mb_CharacterBase user)
        : base(abilityObject, user)
    {
        _cam = Camera.main;
        _Cooldown = _AbilityData.Cooldown;
        projectilePrefab = _AbilityData.ProjectileModel;
    }

    public override void OnEquip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} equipped {_AbilityData.AbilityName}.");
    }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown( )) return;

        LeapBackward(user);
        FireFeatherSpread(user);

        StartCooldown(user);
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} unequipped {_AbilityData.AbilityName}.");
    }

    private void LeapBackward(Mb_CharacterBase user)
    {
        // Camera-relative backward, flattened to XZ
        Vector3 leapDirection = -_cam.transform.forward + Vector3.up;
        leapDirection.Normalize( );

        if (leapDirection == Vector3.zero)
            leapDirection = -user.transform.forward;

        user.Movement.StartDash(leapDirection * leapSpeed, leapDuration);
    }

    private void FireFeatherSpread(Mb_CharacterBase user)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Rajah_E_Ability: No projectile prefab assigned.");
            return;
        }

        Transform originTransform = GameObject.Find("ProjectileOrigin").transform;

        // Resolve the center aim point via screen-center raycast (same as your CreateProjectile)
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 1000f)
            ? hit.point
            : ray.origin + ray.direction * 100f;

        Vector3 centerDirection = ( targetPoint - originTransform.position ).normalized;

        // Spread featherCount projectiles evenly across the cone
        // e.g. 5 feathers at spreadAngle 30°: offsets are -15, -7.5, 0, 7.5, 15
        for (int i = 0; i < featherCount; i++)
        {
            float t = featherCount == 1 ? 0f : (float)i / ( featherCount - 1 ); // 0 to 1
            float angleOffset = Mathf.Lerp(-spreadAngle / 2f, spreadAngle / 2f, t);

            // Rotate centerDirection horizontally by angleOffset degrees
            Vector3 spreadDirection = Quaternion.AngleAxis(angleOffset, Vector3.up) * centerDirection;
            Quaternion spreadRotation = Quaternion.LookRotation(spreadDirection);

            GameObject projectileInstance = GameObject.Instantiate(
                projectilePrefab,
                originTransform.position,
                spreadRotation
            );

            Mb_Projectile mb_Projectile = projectileInstance.GetComponent<Mb_Projectile>( );
            if (mb_Projectile != null)
            {
                // Outer feathers deal slightly less damage — gives the center shot more weight
                float falloff = 1f - ( Mathf.Abs(angleOffset) / spreadAngle ) * 0.3f;
                mb_Projectile.SetDamageAmount(
                    user.AttackPower.Value( ) * _AbilityData.ATKScaling * falloff
                );
            }
        }
    }
}