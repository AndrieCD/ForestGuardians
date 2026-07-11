// Mari_R_Branch1.cs
// [R] Psychic Bloom — Mari's first ultimate branch.
//
// PASSIVE — Blooming DoT:
//   Every Mind Flurry (LMB) hit applies a damage-over-time effect to the enemy
//   struck. The DoT total damage is sourced from the SO "DoT" stat, then split
//   across DOT_DURATION by the generic status-effect controller.
//   Applied fresh on every hit. Psychic Bloom is explicitly stackable, so
//   multiple active DoTs can tick on the same enemy at once.
//   Subscribes to Mari_Primary.OnPrimaryHit — filters by source so only this
//   Mari's hits trigger the passive, not a future second player.
//
// ACTIVE — Psychic Bloom Field:
//   A spherical trigger zone is attached to Mari and follows her position.
//   Enemies inside take damage every FIELD_TICK_INTERVAL seconds and are slowed.
//   The field persists for FIELD_DURATION seconds, then deactivates and destroys itself.
//   Stronger than Q — larger radius, higher damage, Mari is the epicenter.
//
// ZONE PATTERN:
//   Reuses Mb_BloomZone (defined below), which mirrors Mb_MindspikesZone
//   but with a SphereCollider instead of a BoxCollider.
//   Mari herself is not damaged — owner reference is passed for filtering.
//
// VFX:
//   "BloomCastVFX"  — burst particle on Mari at cast time (flower burst / energy)
//   "BloomFieldVFX" — looping particle on the zone GO while active
//   "BloomDoTVFX"   — short burst particle on the enemy hit by DoT tick
//                     (subtle psychic petal/spark per tick)
//   All located by name in OnEquip / prefab hierarchy.
//   TODO: Leo — add these particle systems to Mari's prefab and zone prefab.
//
// PREFAB SETUP (for Leo/Angel):
//   Create a prefab with:
//   - A SphereCollider (isTrigger = true) — radius set dynamically in Initialize()
//   - A Rigidbody (isKinematic = true, useGravity = false)
//   - Attach Mb_BloomZone to the root
//   - A child ParticleSystem named "BloomFieldVFX" (looping petal/bloom aura)
//   - A child mesh GO named "BloomVisualRoot" (translucent sphere, bloom shader)
//     kept at local scale 1; the prefab root is scaled dynamically from FieldRadius
//   Assign prefab to Mari_R_Branch1.BloomZonePrefab in the Inspector.

using System;
using System.Collections;
using UnityEngine;

public class Mari_R_Branch1 : Sc_BaseAbility
{
    private const float DAMAGE_TICK_INTERVAL = 0.5f;

    // -------------------------------------------------------------------------
    // Inspector-Assigned Fields
    // -------------------------------------------------------------------------

    // The spherical bloom zone prefab.
    // Must have: SphereCollider (isTrigger), Rigidbody (kinematic),
    // Mb_BloomZone component, child "BloomFieldVFX" ParticleSystem,
    // child "BloomVisualRoot" mesh GO.
    private GameObject _bloomZonePrefab;

    [Header("Passive — Blooming DoT")]
    // Total duration of the DoT in seconds
    [SerializeField] private float _DotDuration = 2f;

    [Header("Active — Bloom Field")]
    // Radius of the spherical zone in world units
    // TODO: Tune — should feel noticeably larger than Q's rectangle
    [SerializeField] private float _FieldRadius = 20f;

    // How long the field persists in seconds
    [SerializeField] private float _FieldDuration = 4f;

    // Slow duration applied to enemies inside the field each tick.
    // Slow strength is sourced from the SO "Slow" stat.
    [SerializeField] private float _FieldSlowDuration = 1f;


    // -------------------------------------------------------------------------
    // Cached References
    // -------------------------------------------------------------------------

    // Cast burst VFX on Mari — located by name in OnEquip
    private ParticleSystem _castVFX;
    private Mb_HealthComponent _health;
    private Coroutine _bloomRoutine;
    private Mb_PlayerController _activeController;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Mari_R_Branch1(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        _health = user.GetComponent<Mb_HealthComponent>();

        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _bloomZonePrefab = registry?.GetPrefab(AbilityPrefabID.Mari_BloomZone);

        if (_bloomZonePrefab == null)
            Debug.LogError("[Mari_R_Branch1] Mari_BloomZone prefab not found in registry.");

        // Subscribe to Mari_Primary's confirmed hit event for DoT passive
        // Unsubscribe-first prevents duplicate listeners on re-equip
        Mari_Primary.OnPrimaryHit -= HandlePrimaryHit;
        Mari_Primary.OnPrimaryHit += HandlePrimaryHit;

        // Locate cast VFX by name
        foreach (ParticleSystem ps in user.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "BloomCastVFX")
            {
                _castVFX = ps;
                break;
            }
        }

        Debug.Log("[Psychic Bloom] Equipped.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Mari_Primary.OnPrimaryHit -= HandlePrimaryHit;

        if (_bloomRoutine != null)
        {
            user.StopCoroutine(_bloomRoutine);
            _bloomRoutine = null;
            CleanupUltimate(user);
        }

        Debug.Log("[Psychic Bloom] Unequipped.");
    }


    // -------------------------------------------------------------------------
    // Passive — Blooming DoT Handler
    // -------------------------------------------------------------------------

    // Fired by Mari_Primary.OnPrimaryHit on every confirmed LMB hit.
    // target is the MB_CuBotBase hit; we receive damage and source from the event.
    // We need the actual target — so we rely on the projectile's OnHit giving us
    // the target via a separate subscription path. See note below.
    //
    // DESIGN NOTE ON TARGET ACQUISITION:
    //   Mari_Primary.OnPrimaryHit passes (damageDealt, source) but not the target.
    //   To get the target, we subscribe to the projectile's OnHit inside
    //   HandlePrimaryHit via an intermediary. However, since Mari_Primary already
    //   hooks projectile.OnHit internally, the cleanest approach is to extend
    //   the OnPrimaryHit event signature to include the target.
    //
    //   Updated event signature (add to Mari_Primary.cs):
    //   public static event Action<float, Mb_CharacterBase, MB_CuBotBase> OnPrimaryHit;
    //   Fired as: OnPrimaryHit?.Invoke(damage, user, target as MB_CuBotBase);
    //   This is the pattern used below.

    private void HandlePrimaryHit(float damageDealt, Mb_CharacterBase source, MB_CuBotBase target)
    {
        // Filter — only react to this Mari's hits
        if (source != _User) return;
        if (target == null) return;
        if (target.Health == null || target.Health.IsDead) return;

        float totalDotDamage = _AbilityData.GetStat(
            "DoT",
            CurrentLevel,
            _User.Stats.AttackPower.GetValue(),
            _User.Stats.AbilityPower.GetValue()
        );

        totalDotDamage *= damageDealt;

        int tickCount = Mathf.Max(1, Mathf.RoundToInt(_DotDuration / DAMAGE_TICK_INTERVAL));
        float damagePerTick = totalDotDamage / tickCount;

        Mb_StatusEffectController statusController = target.GetComponent<Mb_StatusEffectController>();
        if (statusController == null)
        {
            Debug.LogWarning($"[Psychic Bloom Passive] {target.CharacterName} has no " +
                             "Mb_StatusEffectController. DoT skipped.");
            return;
        }

        statusController.Apply(
            Sc_StatusEffect.PsychicBloom(_DotDuration, damagePerTick, DAMAGE_TICK_INTERVAL)
        );

        Debug.Log($"[Psychic Bloom Passive] DoT applied to {target.CharacterName} — " +
                  $"{damagePerTick:F1} per tick × {tickCount} ticks " +
                  $"({totalDotDamage:F1} total over {_DotDuration}s).");
    }


    // -------------------------------------------------------------------------
    // Active — Activation
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        if (_bloomZonePrefab == null)
        {
            Debug.LogError("[Mari_R_Branch1] BloomZonePrefab is not assigned.");
            return;
        }

        float damagePerSecond = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );
        float damagePerTick = damagePerSecond * DAMAGE_TICK_INTERVAL;

        float slowPercent = _AbilityData.GetStat(
            "Slow",
            CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );

        // Spawn zone as a child of Mari. Mb_BloomZone sets local position and scale
        // from FieldRadius so the collider and visual range stay in sync.
        GameObject zoneGO = GameObject.Instantiate(
            _bloomZonePrefab,
            user.transform
        );

        Mb_BloomZone zone = zoneGO.GetComponent<Mb_BloomZone>();
        if (zone == null)
        {
            Debug.LogError("[Mari_R_Branch1] BloomZonePrefab is missing Mb_BloomZone component.");
            GameObject.Destroy(zoneGO);
            return;
        }

        zone.Initialize(
            owner: user,
            damage: damagePerTick,
            tickInterval: DAMAGE_TICK_INTERVAL,
            slowPercent: slowPercent,
            slowDuration: _FieldSlowDuration,
            radius: _FieldRadius,
            duration: _FieldDuration
        );

        // Cast VFX on Mari
        _castVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _castVFX?.Play();

        _activeController = user as Mb_PlayerController;

        TriggerAbilityAnimation(user);

        _bloomRoutine = user.StartCoroutine(BloomRoutine(user));

        StartCooldown(user, GetAbilityCooldown(user));
    }


    private IEnumerator BloomRoutine(Mb_CharacterBase user)
    {
        SetInvulnerable(true);

        _activeController?.AddDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );

        yield return new WaitForSeconds(_FieldDuration);

        CleanupUltimate(user);
        _bloomRoutine = null;
    }


    private void CleanupUltimate(Mb_CharacterBase user)
    {
        SetInvulnerable(false);

        _activeController?.RemoveDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );
        _activeController = null;

        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.EndR1Ability();
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
            guardian.GuardianAnimator?.TriggerR1Ability();
    }
}
