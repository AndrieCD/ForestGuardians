// Sc_BernieFireCone.cs
// Bernie's primary attack — a wide fire cone burst fired from a child origin point.
//
// ATTACK SHAPE:
//   Physics.OverlapSphere centered on the fire point, filtered by a forward dot
//   product check to fake a cone shape. Anything within CONE_HALF_ANGLE degrees
//   of Bernie's forward direction gets hit.
//
// DAMAGE:
//   Direct damage from SO "Damage" stat entry, scaled with ATK.
//   Burn DoT applied via Mb_StatusEffectController — damage per tick is
//   the SO "BurnDamage" stat entry amplified by Bernie's Haste stat.
//   Burn duration is a flat value from the SO "BurnDuration" entry.
//
// COOLDOWN:
//   Uses GetAttackCooldown() — driven by AttackSpeed, same as Chopper.
//   At AS 1.0, fires once per second.
//
// VFX:
//   CuBot_Bernie_Attack — plays at the fire point origin on every activation.
//   CuBot_Bernie_Hit    — plays at each hit target's position.
//
// SFX:
//   Bernie_FireCone — plays at the fire point on activation.
//   Bernie_Hit      — plays at each hit target's position.
//
// Inspector setup (on the Bernie prefab):
//   - Assign a child Transform named "FirePoint" at the flamethrower nozzle height.
//   - Wire it to Mb_BernieController._FirePoint in the Inspector.
//   - SO_Ability needs three scaling entries:
//       "Damage"       — scales with ATK
//       "BurnDamage"   — scales with ATK, multiplied by Haste factor at cast time
//       "BurnDuration" — flat seconds, leave ATK and AP columns at 0

using UnityEngine;

public class Sc_BernieFireCone : Sc_BaseAbility
{
    // Radius of the overlap sphere — how far the flames reach from the fire point.
    // TODO: Tune to match Bernie's flamethrower visual range.
    private const float CONE_RADIUS = 4f;

    // Half-angle of the fire cone in degrees.
    // 45f means anything within 45° left or right of Bernie's forward is hit.
    // TODO: Tune — wider feels more threatening, narrower rewards the player for flanking.
    private const float CONE_HALF_ANGLE = 45f;

    // The child transform on Bernie's prefab marking the nozzle of the flamethrower.
    // Passed in from the controller so the ability doesn't do a scene search.
    private readonly Transform _firePoint;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Sc_BernieFireCone(
        SO_Ability abilityData,
        Mb_CharacterBase user,
        Transform firePoint)
        : base(abilityData, user)
    {
        _firePoint = firePoint;

        if (_firePoint == null)
            Debug.LogError("[Sc_BernieFireCone] FirePoint transform is null. " +
                           "Assign it on Mb_BernieController in the Inspector.");
    }


    // -------------------------------------------------------------------------
    // Activation
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        user.GetComponent<Mb_CuBotAnimator>()?.TriggerAttack();

        FireCone(user);

        StartCooldown(user, GetAttackCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Fire Cone Logic
    // -------------------------------------------------------------------------

    private void FireCone(Mb_CharacterBase user)
    {
        Vector3 origin = _firePoint != null
            ? _firePoint.position
            : user.transform.position + Vector3.up * 1.0f;

        //VFX — fire burst at the nozzle origin
       Mb_VFXManager.Play(
           VFXType.CuBot_Bernie_Attack,
           origin
       );

        // SFX — flamethrower whoosh at the nozzle
        //Mb_AudioManager.PlaySFX(CombatSFX.Bernie_FireCone, origin);

        // Calculate damage values at fire time so they're consistent
        // across all targets hit by the same burst
        float directDamage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue()
        );

        // Burn damage amplified by Bernie's AP
        float burnDamagePerTick = _AbilityData.GetStat(
            "BurnDamage",
            CurrentLevel,
            user.Stats.AbilityPower.GetValue());

        // Scales with haste
        float burnDuration = _AbilityData.GetStat("BurnDuration", CurrentLevel) * (1f + user.Stats.Haste.GetValue() / 100f);

        // Find everything in the sphere and filter to the cone
        Collider[] hits = Physics.OverlapSphere(origin, CONE_RADIUS);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == user.gameObject) continue;
            if (hit.gameObject.CompareTag("CuBot")) continue;

            //// Cone filter — dot product between Bernie's forward and the direction
            //// to the target. A dot > cos(halfAngle) means the target is inside the cone.
            //Vector3 dirToTarget = (hit.transform.position - origin).normalized;
            //float dot = Vector3.Dot(user.transform.forward, dirToTarget);
            //float cosHalfAngle = Mathf.Cos(CONE_HALF_ANGLE * Mathf.Deg2Rad);

            //if (dot < cosHalfAngle) continue; // Outside the cone — skip

            // Apply direct damage
            I_Damageable damageable = hit.GetComponent<I_Damageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(directDamage);

            // Apply Burn DoT via status effect controller —
            // reapplication refreshes duration rather than stacking (handled by controller)
            Mb_StatusEffectController statusController =
                hit.GetComponent<Mb_StatusEffectController>();

            if (statusController != null)
            {
                statusController.Apply(
                    Sc_StatusEffect.Burn(burnDuration, burnDamagePerTick)
                );
            }

            // VFX — ignition flash on the hit target
            //Mb_VFXManager.Play(
            //    VFXType.CuBot_Bernie_Hit,
            //    hit.transform.position,
            //    parent: null
            //);

            // SFX — ignition crack at the target's position
            //Mb_AudioManager.PlaySFX(CombatSFX.Bernie_Hit, hit.transform.position);
        }
    }
}