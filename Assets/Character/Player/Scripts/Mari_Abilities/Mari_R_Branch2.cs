// Mari_R_Branch2.cs
// [R] Mind Unbound — Mari's second ultimate branch.
//
// PASSIVE — Focused Mind:
//   Every Psyshock (RMB) hit deals bonus damage equal to the SO "APBonusPercent"
//   stat multiplied by Mari's current AbilityPower as flat bonus damage on the
//   direct hit target. Additionally, the SO "SplashPercent" stat controls how
//   much of the total damage dealt (base + AP bonus)
//   is dealt to all OTHER enemies within SPLASH_RADIUS of the hit target.
//
//   Subscribes to Mari_Secondary.OnSecondaryFired — this fires before the
//   projectile hits anything, so we subscribe to the returned projectile's
//   OnHit event to react on confirmed impact only.
//
//   SPLASH TARGET EXCLUSION:
//   The directly hit enemy is excluded from splash — they already took full damage.
//   Splash uses Physics.OverlapSphere centered on the hit point.
//
// ACTIVE — Laser:
//   On cast, Mari is locked (AllAbilities + AllAttacks disabled) and a
//   continuous thick beam fires from ProjectileOrigin forward every frame for
//   LASER_DURATION (3s). Every enemy inside the beam path takes damage from
//   the SO "Damage" stat on a fixed 0.5-second tick.
//   The beam re-evaluates each frame so it tracks Mari's aim as she turns.
//
//   LASER VISUALS:
//   A LineRenderer component on the laser GO draws the beam from
//   ProjectileOrigin to the terrain hit point (or MaxRange if no hit).
//   A "LaserHitVFX" ParticleSystem plays at the impact point while the
//   laser is in contact with an enemy — moved to hit position each frame.
//   A "LaserChargeVFX" burst plays on Mari at cast time (glasses-off moment).
//
// VFX SUMMARY:
//   On Mari prefab (located by name in OnEquip):
//   - "LaserChargeVFX"    — burst at cast time (glasses-off charge up)
//   - "LaserEyeVFX"       — looping glow from Mari's eyes during laser (optional)
//   On laser GO prefab:
//   - LineRenderer         — draws the beam, configured in Initialize()
//   - "LaserHitVFX"       — looping spark/burn at the impact point, moved per frame
//   Assign laser GO prefab to Mari_R_Branch2.LaserPrefab in Inspector.
//
// PREFAB SETUP (for Leo/Angel):
//   Create a prefab with:
//   - A LineRenderer (2 positions: origin + hit point, updated each frame)
//     Set width curve, material (additive psychic beam), and color in the Editor.
//   - A child ParticleSystem named "LaserHitVFX" (looping sparks at impact point)
//   - Attach Mb_LaserBeam to the root
//   The prefab has no Collider or Rigidbody — detection is handled by script.
//
// INSPECTOR SETUP:
//   LaserPrefab            — the laser GO prefab
//   SplashRadius           — radius of splash around hit point          (default 3f)
//   LaserDuration          — how long the active laser fires            (default 3f)
//   LaserMaxRange          — max raycast distance                       (default 30f)
//   LaserRadius            — beam hit radius and visual half-width      (default 0.75f)
//   LaserLayerMask         — layers the laser can hit                   (Enemies + Environment)

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mari_R_Branch2 : Sc_BaseAbility
{
    private const float DAMAGE_TICK_INTERVAL = 0.25f;

    // -------------------------------------------------------------------------
    // Inspector-Assigned Fields
    // -------------------------------------------------------------------------

    // The laser beam GO prefab.
    // Must have: LineRenderer, child "LaserHitVFX" ParticleSystem,
    // Mb_LaserBeam component on root.
    private GameObject _laserPrefab;

    [Header("Passive — Focused Mind")]
    // Radius of the splash area around the hit target's position
    // TODO: Tune to feel meaningful without being overpowered
    [SerializeField] private float _SplashRadius = 3f;

    [Header("Active — Laser")]
    // How long the laser fires in seconds
    [SerializeField] private float _LaserDuration = 3f;

    // Maximum distance for the laser beam
    [SerializeField] private float _LaserMaxRange = 30f;

    // Beam radius used for enemy detection and LineRenderer width.
    [SerializeField] private float _LaserRadius = 1.0f;

    // Layer mask for the laser beam — should include Enemy and Environment layers
    // TODO: Set in Inspector — include "CuBot" and terrain layers, exclude "Player"
    [SerializeField] private LayerMask _LaserLayerMask = Physics.DefaultRaycastLayers;


    // -------------------------------------------------------------------------
    // Cached References
    // -------------------------------------------------------------------------

    // VFX on Mari — located by name in OnEquip
    private ParticleSystem _chargeVFX;
    private ParticleSystem _eyeGlowVFX;
    private Mb_HealthComponent _health;
    private Mb_PlayerController _activeController;
    private Mb_LaserBeam _activeLaser;

    // Handle to the running laser coroutine so we can cancel on unequip
    private Coroutine _laserRoutine;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Mari_R_Branch2(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        _health = user.GetComponent<Mb_HealthComponent>();

        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _laserPrefab = registry?.GetPrefab(AbilityPrefabID.Mari_LaserBeam);

        if (_laserPrefab == null)
            Debug.LogError("[Mari_R_Branch2] Mari_LaserBeam prefab not found in registry.");


        // Subscribe to Psyshock fired event for passive
        // Unsubscribe-first prevents duplicate listeners on re-equip
        Mari_Secondary.OnSecondaryFired -= HandleSecondaryFired;
        Mari_Secondary.OnSecondaryFired += HandleSecondaryFired;

        // Locate VFX by name on Mari's prefab
        foreach (ParticleSystem ps in user.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "LaserChargeVFX") _chargeVFX = ps;
            if (ps.gameObject.name == "LaserEyeVFX") _eyeGlowVFX = ps;
        }

        Debug.Log("[Mind Unbound] Equipped.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Mari_Secondary.OnSecondaryFired -= HandleSecondaryFired;

        // Cancel laser if it's still running (e.g. guardian died mid-beam)
        if (_laserRoutine != null)
        {
            user.StopCoroutine(_laserRoutine);
            _laserRoutine = null;
            CleanupLaserUltimate(user);
        }

        _eyeGlowVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Debug.Log("[Mind Unbound] Unequipped.");
    }


    // -------------------------------------------------------------------------
    // Passive — Focused Mind
    // -------------------------------------------------------------------------

    // Receives the fired event from Mari_Secondary immediately after the
    // projectile is launched. We subscribe to the projectile's OnHit here
    // so splash only fires on confirmed contact, not on fire.
    private void HandleSecondaryFired(
        Mb_CharacterBase source,
        Vector3 origin,
        Vector3 direction,
        Mb_Projectile projectile)
    {
        // Filter — only react to this Mari's shots
        if (source != _User) return;
        if (projectile == null) return;

        projectile.OnHit += (target, hitPoint, hitNormal) =>
        {
            MB_CuBotBase directHitEnemy = target as MB_CuBotBase;
            ApplySplashPassive(projectile, directHitEnemy, hitPoint);
        };
    }


    // Applies AP bonus to the direct hit (via the projectile data) and
    // splash damage to all nearby enemies excluding the direct hit target.
    private void ApplySplashPassive(Mb_Projectile projectile, MB_CuBotBase directHit, Vector3 hitPoint)
    {
        if (projectile == null) return;

        // --- AP Bonus on direct hit ---
        // The projectile already carried its base damage and dealt it in OnTriggerEnter.
        // The AP bonus is additional damage applied here on top of that.
        float apBonusPercent = _AbilityData.GetStat(
            "APBonusPercent",
            CurrentLevel,
            _User.Stats.AttackPower.GetValue(),
            _User.Stats.AbilityPower.GetValue()
        );
        float apBonus = _User.Stats.AbilityPower.GetValue() * apBonusPercent;

        if (directHit != null && !directHit.Health.IsDead)
        {
            directHit.Health.TakeDamage(apBonus);
            Debug.Log($"[Mind Unbound Passive] AP bonus {apBonus:F1} applied to " +
                      $"{directHit.CharacterName}.");
        }

        // Total damage for splash calculation = base projectile damage + AP bonus
        float baseDamage = projectile.GetDamageAmount();
        float totalDamage = baseDamage + apBonus;
        float splashPercent = _AbilityData.GetStat(
            "SplashPercent",
            CurrentLevel,
            _User.Stats.AttackPower.GetValue(),
            _User.Stats.AbilityPower.GetValue()
        );
        float splashDamage = totalDamage * splashPercent;

        // --- Splash damage to nearby enemies ---
        Vector3 splashOrigin = hitPoint;
        Collider[] nearby = Physics.OverlapSphere(splashOrigin, _SplashRadius);
        HashSet<MB_CuBotBase> damagedSplashTargets = new HashSet<MB_CuBotBase>();

        foreach (Collider col in nearby)
        {
            MB_CuBotBase splashTarget = col.GetComponentInParent<MB_CuBotBase>();
            if (splashTarget == null) continue;
            if (!damagedSplashTargets.Add(splashTarget)) continue;
            if (splashTarget.Health == null || splashTarget.Health.IsDead) continue;

            // Exclude the directly hit enemy — they already took full damage
            if (directHit != null && splashTarget == directHit) continue;

            splashTarget.Health.TakeDamage(splashDamage);

            Debug.Log($"[Mind Unbound Passive] Splash {splashDamage:F1} to " +
                      $"{splashTarget.CharacterName}.");
        }
    }


    // -------------------------------------------------------------------------
    // Active — Laser Beam
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        if (_laserPrefab == null)
        {
            Debug.LogError("[Mari_R_Branch2] LaserPrefab is not assigned.");
            return;
        }

        float laserDps = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );

        // Spawn the laser GO — it lives for the duration of the beam
        GameObject laserGO = GameObject.Instantiate(
            _laserPrefab,
            Vector3.zero,   // Positioned each frame by Mb_LaserBeam.UpdateBeam()
            Quaternion.identity
        );

        Mb_LaserBeam laser = laserGO.GetComponent<Mb_LaserBeam>();
        if (laser == null)
        {
            Debug.LogError("[Mari_R_Branch2] LaserPrefab is missing Mb_LaserBeam component.");
            GameObject.Destroy(laserGO);
            return;
        }

        laser.Initialize(
            owner: user,
            origin: _Guardian.ProjectileOrigin,
            dps: laserDps,
            tickInterval: DAMAGE_TICK_INTERVAL,
            maxRange: _LaserMaxRange,
            radius: _LaserRadius,
            layerMask: _LaserLayerMask
        );

        // Lock input for the laser duration
        _activeController = user as Mb_PlayerController;
        _activeLaser = laser;

        _activeController?.AddDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );

        SetInvulnerable(true);

        // Charge VFX — glasses-off moment
        _chargeVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _chargeVFX?.Play();

        // Eye glow VFX — looping while laser fires
        _eyeGlowVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _eyeGlowVFX?.Play();

        TriggerAbilityAnimation(user);

        _laserRoutine = user.StartCoroutine(
            LaserRoutine(user, laser)
        );

        StartCooldown(user, GetAbilityCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Laser Coroutine
    // -------------------------------------------------------------------------

    private IEnumerator LaserRoutine(
        Mb_CharacterBase user,
        Mb_LaserBeam laser)
    {
        float elapsed = 0f;

        while (elapsed < _LaserDuration)
        {
            elapsed += Time.deltaTime;

            // Tell the laser beam to fire this frame — it handles raycast,
            // damage application, LineRenderer update, and hit VFX positioning
            laser.FireThisFrame();

            yield return null;
        }

        CleanupLaserUltimate(user);

        _laserRoutine = null;

        Debug.Log("[Mind Unbound] Laser ended.");
    }


    private void CleanupLaserUltimate(Mb_CharacterBase user)
    {
        _activeLaser?.Deactivate();
        _activeLaser = null;

        _activeController?.RemoveDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );
        _activeController = null;

        SetInvulnerable(false);

        _eyeGlowVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.EndR2Ability();
    }


    private void SetInvulnerable(bool state)
    {
        if (_health == null) return;

        _health.IsInvulnerable = state;
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
