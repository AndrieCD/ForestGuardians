using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [Q]: Rajah dashes forward, damaging enemies he passes through.
/// Each enemy hit grants a stack of shield that decays after a duration.
/// </summary>
public class Rajah_Q_Ability : Sc_BaseAbility
{
    private Camera _cam;

    // --- Dash tuning ---
    public float dashSpeed = 80f;
    public float dashDuration = 0.175f;

    // --- Hit detection tuning ---
    // The overlap sphere radius around Rajah while dashing — adjust to match his character width
    private float _hitRadius = 1.2f;

    // --- Shield tuning ---
    // How long the shield lasts before it disappears (in seconds)
    private float _shieldDuration = 4f;
    // Each enemy hit contributes 25% of the base shield value
    private float _shieldPerEnemyFraction = 0.25f;

    // Chapter 3 Table 8 — Sky Rend shield values per ability level
    // Index 0 = level 1, index 5 = level 6
    // TODO: Tie this to actual ability level once the upgrade system is in
    private float[] _baseShieldPerLevel = { 150f, 200f, 250f, 300f, 350f, 400f };

    // Chapter 3 Table 8 — Sky Rend AP shield scaling per level
    private float[] _apShieldScalingPerLevel = { 0.70f, 0.85f, 1.00f, 1.15f, 1.50f, 2.00f };

    // Tracks enemies already hit during this dash so we don't damage them twice
    private HashSet<Collider> _hitThisDash = new HashSet<Collider>( );

    public Rajah_Q_Ability(SO_Ability abilityObject, Mb_CharacterBase user)
        : base(abilityObject, user)
    {
        _cam = Camera.main;
        _Cooldown = _AbilityData.Cooldown;
    }

    public override void OnEquip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} equipped {_AbilityData.AbilityName}.");
    }

    // Called when the player presses [Q]
    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown( )) return;

        // Flatten camera forward to XZ so we dash horizontally, not into the ground
        Vector3 dashDirection = _cam.transform.forward;
        dashDirection.y = 0f;
        dashDirection.Normalize( );

        if (dashDirection == Vector3.zero)
            dashDirection = user.transform.forward;

        // Start the movement
        user.Movement.StartDash(dashDirection * dashSpeed, dashDuration);

        // Start polling for hits during the dash — needs to run on a MonoBehaviour
        user.StartCoroutine(DashHitRoutine(user, dashDuration));

        StartCooldown(user);

        Debug.Log($"{user.name} activated {_AbilityData.AbilityName}.");
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} unequipped {_AbilityData.AbilityName}.");
    }

    // -------------------------------------------------------

    // Runs every frame while the dash is active.
    // Checks for enemies overlapping Rajah's position and damages each one once.
    // After the dash ends, calculates and applies the shield based on total enemies hit.
    private IEnumerator DashHitRoutine(Mb_CharacterBase user, float duration)
    {
        _hitThisDash.Clear( );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Check for any CuBot colliders touching Rajah's current position
            Collider[] nearbyColliders = Physics.OverlapSphere(user.transform.position, _hitRadius);

            foreach (Collider col in nearbyColliders)
            {
                // Skip if we already hit this enemy this dash
                if (_hitThisDash.Contains(col)) continue;

                MB_CuBotBase cuBot = col.GetComponent<MB_CuBotBase>( );
                if (cuBot == null) continue;

                // Deal damage — Sky Rend uses ATK and AP scaling from the SO
                float damage = _AbilityData.GetStat("Damage", _currentAbilityLevel, user.AttackPower.Value( ), user.AbilityPower.Value( ));

                cuBot.TakeDamage(damage);
                _hitThisDash.Add(col); // Mark as hit so we don't hit them again mid-dash

                Debug.Log($"Sky Rend hit {col.name} for {damage} damage.");
            }

            elapsed += Time.deltaTime;
            yield return null; // Wait one frame, then check again
        }

        // Dash is over — now reward shield based on how many enemies were hit
        ApplyShield(user, _hitThisDash.Count);
    }

    private void ApplyShield(Mb_CharacterBase user, int enemiesHit)
    {
        if (enemiesHit == 0) return;

        // TODO: Replace index 0 with the actual ability level once upgrade system exists
        int levelIndex = 0;

        float baseShield = _baseShieldPerLevel[levelIndex];
        float apScaling = _apShieldScalingPerLevel[levelIndex];

        // Additional shielding based on enemies hit
        float bonusShield = baseShield * (enemiesHit * _shieldPerEnemyFraction);
        float shieldAmount = bonusShield + _AbilityData.GetStat("Shield", _currentAbilityLevel, 0f, user.AbilityPower.Value( ));

        // Apply shield as a temporary additive effect on the Shielding stat
        Sc_StatEffect shieldEffect = new Sc_StatEffect(StatType.Shielding, shieldAmount, StatModType.Flat, _shieldDuration);

        // Apply shield modifier to the user
        Sc_Modifier shieldModifier = new Sc_Modifier("Sky Rend Shield", 
            new List<Sc_StatEffect> { shieldEffect}, 
            new Dictionary<StatType, Sc_Stat> { { StatType.Shielding, user.Shielding } },
            user, _shieldDuration);


        Debug.Log($"Sky Rend granted {shieldAmount} shield from {enemiesHit} enemies hit.");
    }
}