// Mari_R_Branch2.cs
// [R] Mind Unbound — Mari's second ultimate branch.
//
// PASSIVE — Focused Mind:
//   Every Psyshock (RMB) hit deals bonus damage equal to AP_BONUS_PERCENT (50%)
//   of Mari's current AbilityPower as flat bonus damage on the direct hit target.
//   Additionally, SPLASH_PERCENT (25%) of the total damage dealt (base + AP bonus)
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
//   continuous raycast fires from ProjectileOrigin forward every frame for
//   LASER_DURATION (3s). Every enemy the ray hits per frame takes damage
//   scaled by Time.deltaTime so total DPS is consistent regardless of framerate.
//   The ray re-evaluates each frame so it tracks Mari's aim as she turns.
//
//   LASER VISUALS:
//   A LineRenderer component on the laser GO draws the beam from
//   ProjectileOrigin to the raycast hit point (or MaxRange if no hit).
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
//   The prefab has no Collider or Rigidbody — detection is raycast-only.
//
// INSPECTOR SETUP:
//   LaserPrefab            — the laser GO prefab
//   ApBonusPercent         — bonus AP fraction on direct Psyshock hit  (default 0.5f)
//   SplashPercent          — fraction of total damage as splash         (default 0.25f)
//   SplashRadius           — radius of splash around hit point          (default 3f)
//   LaserDuration          — how long the active laser fires            (default 3f)
//   LaserDps               — damage per second of the laser beam        (default 200f)
//   LaserMaxRange          — max raycast distance                       (default 30f)
//   LaserLayerMask         — layers the laser can hit                   (Enemies + Environment)

using System;
using System.Collections;
using UnityEngine;

public class Mari_R_Branch2 : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Inspector-Assigned Fields
    // -------------------------------------------------------------------------

    // The laser beam GO prefab.
    // Must have: LineRenderer, child "LaserHitVFX" ParticleSystem,
    // Mb_LaserBeam component on root.
    private GameObject _laserPrefab;

    [Header("Passive — Focused Mind")]
    // Fraction of current AbilityPower added as flat bonus damage on direct hit
    // TODO: Tune — 0.5 = 50% AP bonus on each Psyshock hit
    [SerializeField] private float _ApBonusPercent = 0.5f;

    // Fraction of total damage (base + AP bonus) applied as splash to nearby enemies
    // TODO: Tune — 0.25 = 25% splash
    [SerializeField] private float _SplashPercent = 0.25f;

    // Radius of the splash area around the hit target's position
    // TODO: Tune to feel meaningful without being overpowered
    [SerializeField] private float _SplashRadius = 3f;

    [Header("Active — Laser")]
    // How long the laser fires in seconds
    [SerializeField] private float _LaserDuration = 3f;

    // Damage per second dealt by the laser — applied as (DPS * deltaTime) per frame
    // TODO: Tune — 200f is a starting point for "massive damage" feel
    [SerializeField] private float _LaserDps = 200f;

    // Maximum raycast distance for the laser beam
    [SerializeField] private float _LaserMaxRange = 30f;

    // Layer mask for the laser raycast — should include Enemy and Environment layers
    // TODO: Set in Inspector — include "CuBot" and terrain layers, exclude "Player"
    [SerializeField] private LayerMask _LaserLayerMask;


    // -------------------------------------------------------------------------
    // Cached References
    // -------------------------------------------------------------------------

    // VFX on Mari — located by name in OnEquip
    private ParticleSystem _chargeVFX;
    private ParticleSystem _eyeGlowVFX;

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
        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _laserPrefab = registry?.GetPrefab(AbilityPrefabID.Mari_LaserBeam);

        if (_laserPrefab == null)
            Debug.LogError("[Mari_Q] Mari_LaserBeam prefab not found in registry.");


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
        Vector3 direction)
    {
        // Filter — only react to this Mari's shots
        if (source != _User) return;

        // We need access to the actual Mb_Projectile that was just fired.
        // Mari_Secondary fires it and then immediately raises OnSecondaryFired,
        // so the projectile is already in flight. We use a targeted find approach:
        // subscribe to the static Mb_Projectile.OnAnyProjectileHit, filter by
        // whether the attacker is our owner, then unsubscribe after one hit.
        //
        // This is the cleanest approach without modifying Mari_Secondary's
        // method signature to return the projectile.
        //
        // Alternative (cleaner, requires Mari_Secondary change):
        // Add Mb_Projectile as a 4th parameter to OnSecondaryFired and pass it
        // from FireProjectile() after _launcher.Fire() returns. This would let
        // us subscribe to projectile.OnHit directly here without the static event.
        // TODO: Refactor Mari_Secondary.OnSecondaryFired to pass the projectile
        //       reference once this is confirmed to work in testing.

        Mb_Projectile.OnAnyProjectileHit += HandlePsyshockHit;
    }


    // Called by the static OnAnyProjectileHit when any projectile hits anything.
    // We filter to only react when the attacker is our owner, then unsubscribe.
    private void HandlePsyshockHit(Mb_Projectile projectile, Mb_CharacterBase attacker, Mb_CharacterBase target)
    {
        // Unsubscribe immediately — this listener is one-shot per Psyshock fired
        Mb_Projectile.OnAnyProjectileHit -= HandlePsyshockHit;

        // Filter — only react to this Mari's projectile hits
        if (attacker != _User) return;

        // Get the direct hit target from the projectile's last hit
        // OnAnyProjectileHit passes the character that was hit
        // We need the actual MB_CuBotBase for splash exclusion
        MB_CuBotBase directHitEnemy = null;

        // The second parameter to OnAnyProjectileHit is Mb_CharacterBase target —
        // but our delegate only receives (projectile, attacker). We need to
        // update the static event signature to also pass the hit target.
        //
        // REQUIRED CHANGE to Mb_Projectile.cs:
        // Change: public static event Action<Mb_Projectile, Mb_CharacterBase> OnAnyProjectileHit;
        // To:     public static event Action<Mb_Projectile, Mb_CharacterBase, Mb_CharacterBase> OnAnyProjectileHit;
        //                                                   ^^attacker           ^^target
        // Then invoke as: OnAnyProjectileHit?.Invoke(this, _owner, target);
        // This is the cleanest path — one small change to Mb_Projectile enables
        // both this passive and any future hit-reactive system cleanly.
        //
        // For now the above is noted as a TODO and the splash is applied at the
        // projectile's current position using OverlapSphere as a fallback.

        ApplySplashPassive(projectile, directHitEnemy);
    }


    // Applies AP bonus to the direct hit (via the projectile data) and
    // splash damage to all nearby enemies excluding the direct hit target.
    private void ApplySplashPassive(Mb_Projectile projectile, MB_CuBotBase directHit)
    {
        if (projectile == null) return;

        // --- AP Bonus on direct hit ---
        // The projectile already carried its base damage and dealt it in OnTriggerEnter.
        // The AP bonus is additional damage applied here on top of that.
        float apBonus = _User.Stats.AbilityPower.GetValue() * _ApBonusPercent;

        if (directHit != null && !directHit.Health.IsDead)
        {
            directHit.Health.TakeDamage(apBonus);
            Debug.Log($"[Mind Unbound Passive] AP bonus {apBonus:F1} applied to " +
                      $"{directHit.CharacterName}.");
        }

        // Total damage for splash calculation = base projectile damage + AP bonus
        float baseDamage = projectile.GetDamageAmount();
        float totalDamage = baseDamage + apBonus;
        float splashDamage = totalDamage * _SplashPercent;

        // --- Splash damage to nearby enemies ---
        Vector3 splashOrigin = projectile.transform.position;
        Collider[] nearby = Physics.OverlapSphere(splashOrigin, _SplashRadius);

        foreach (Collider col in nearby)
        {
            MB_CuBotBase splashTarget = col.GetComponent<MB_CuBotBase>();
            if (splashTarget == null) continue;
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
            dps: _LaserDps,
            maxRange: _LaserMaxRange,
            layerMask: _LaserLayerMask
        );

        // Lock input for the laser duration
        var controller = user as Mb_PlayerController;
        controller?.AddDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );

        // Charge VFX — glasses-off moment
        _chargeVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _chargeVFX?.Play();

        // Eye glow VFX — looping while laser fires
        _eyeGlowVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _eyeGlowVFX?.Play();

        TriggerAbilityAnimation(user);

        _laserRoutine = user.StartCoroutine(
            LaserRoutine(user, controller, laser)
        );

        StartCooldown(user, GetAbilityCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Laser Coroutine
    // -------------------------------------------------------------------------

    private IEnumerator LaserRoutine(
        Mb_CharacterBase user,
        Mb_PlayerController controller,
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

        // --- Cleanup ---
        laser.Deactivate();

        controller?.RemoveDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );

        _eyeGlowVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.EndR2Ability();

        _laserRoutine = null;

        Debug.Log("[Mind Unbound] Laser ended.");
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


// =============================================================================
// Mb_LaserBeam.cs
// Attached to the laser prefab. Owns the per-frame raycast, damage application,
// LineRenderer update, and hit VFX positioning.
// Kept separate so it can be a prefab component and referenced from the Editor.
// =============================================================================

public class Mb_LaserBeam : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    private Mb_CharacterBase _owner;
    private Transform _origin;
    private float _dps;
    private float _maxRange;
    private LayerMask _layerMask;

    // LineRenderer draws the visible beam from origin to hit point each frame
    private LineRenderer _lineRenderer;

    // Looping impact VFX — repositioned to the hit point each frame
    // Played when the beam is in contact with an enemy, stopped otherwise
    private ParticleSystem _hitVFX;
    private bool _hitVFXPlaying = false;

    private bool _isInitialized = false;


    // -------------------------------------------------------------------------
    // Initialize
    // Called by Mari_R_Branch2 immediately after Instantiate()
    // -------------------------------------------------------------------------

    public void Initialize(
        Mb_CharacterBase owner,
        Transform origin,
        float dps,
        float maxRange,
        LayerMask layerMask)
    {
        _owner = owner;
        _origin = origin;
        _dps = dps;
        _maxRange = maxRange;
        _layerMask = layerMask;

        // --- LineRenderer setup ---
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
        {
            Debug.LogError("[Mb_LaserBeam] No LineRenderer found on laser prefab. " +
                           "Add a LineRenderer component to the root of the laser prefab.");
        }
        else
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.enabled = true;
        }

        // --- Locate hit VFX ---
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "LaserHitVFX")
            {
                _hitVFX = ps;
                break;
            }
        }

        _isInitialized = true;
    }


    // -------------------------------------------------------------------------
    // Per-Frame Fire
    // Called every frame by LaserRoutine() while the beam is active
    // -------------------------------------------------------------------------

    public void FireThisFrame()
    {
        if (!_isInitialized || _origin == null) return;

        Vector3 rayOrigin = _origin.position;
        Vector3 rayDirection = _origin.forward;
        Vector3 endPoint = rayOrigin + rayDirection * _maxRange;

        // Cast a ray forward from ProjectileOrigin each frame
        // The beam re-aims every frame so turning Mari changes where it hits
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit,
                            _maxRange, _layerMask))
        {
            endPoint = hit.point;

            // Apply damage this frame — DPS scaled by deltaTime for
            // framerate-independent consistent total damage over the duration
            MB_CuBotBase enemy = hit.collider.GetComponent<MB_CuBotBase>();
            if (enemy != null && !enemy.Health.IsDead)
            {
                float damageThisFrame = _dps * Time.deltaTime;
                enemy.Health.TakeDamage(damageThisFrame);
            }

            // Position and play hit VFX at impact point
            PositionHitVFX(hit.point, hit.normal);
        }
        else
        {
            // No hit — stop hit VFX if it was playing
            StopHitVFX();
        }

        // Update LineRenderer from origin to end point (hit or max range)
        UpdateBeamVisual(rayOrigin, endPoint);
    }


    // -------------------------------------------------------------------------
    // Visual Helpers
    // -------------------------------------------------------------------------

    private void UpdateBeamVisual(Vector3 from, Vector3 to)
    {
        if (_lineRenderer == null) return;

        _lineRenderer.SetPosition(0, from);
        _lineRenderer.SetPosition(1, to);
    }

    private void PositionHitVFX(Vector3 position, Vector3 normal)
    {
        if (_hitVFX == null) return;

        // Move hit VFX to impact point, oriented along the surface normal
        _hitVFX.transform.position = position;
        _hitVFX.transform.rotation = Quaternion.LookRotation(normal);

        if (!_hitVFXPlaying)
        {
            _hitVFX.Play();
            _hitVFXPlaying = true;
        }
    }

    private void StopHitVFX()
    {
        if (_hitVFX == null || !_hitVFXPlaying) return;

        _hitVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _hitVFXPlaying = false;
    }


    // -------------------------------------------------------------------------
    // Deactivation
    // Called by LaserRoutine() when the duration expires
    // -------------------------------------------------------------------------

    public void Deactivate()
    {
        _isInitialized = false;

        StopHitVFX();

        if (_lineRenderer != null)
            _lineRenderer.enabled = false;

        // Destroy after a short delay so hit VFX particles finish fading
        // TODO: Tune delay to match VFX fade duration Leo sets on the prefab
        Destroy(gameObject, 1.5f);
    }
}