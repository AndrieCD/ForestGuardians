// Mari_Q.cs
// [Q] Mindspikes — Mari erupts psychic spikes in a rectangular field in front of her.
//
// BEHAVIOR:
//   On cast, a rectangular trigger zone is created starting at Mari's feet and
//   stretching forward along her facing direction. Enemies inside the zone are
//   damaged every DAMAGE_TICK_INTERVAL seconds and slowed for SLOW_DURATION seconds.
//   The zone persists for SPIKE_DURATION seconds, then deactivates itself.
//
// RECTANGLE DIMENSIONS (normal):
//   Length  = SpikeLength  (forward, Z axis of the spawned GO)
//   Width   = SpikeWidth   (lateral, X axis of the spawned GO)
//   Height  = SpikeHeight  (fixed, Y axis — enough to catch all enemies on terrain)
//
//   The GO is positioned so its back edge starts at Mari's feet:
//     spawnPos = Mari.position + Mari.forward * (SpikeLength / 2f)
//   This means the center of the box sits (SpikeLength / 2) ahead of Mari,
//   making the back edge flush with her position.
//
// OVERCHARGE (from Mari_Passive.IsOvercharged):
//   - Damage per tick × OverchargeDamageMultiplier     (default 2x)
//   - Slow percent  × OverchargeSlowMultiplier         (default 2x)
//   - SpikeLength   × OverchargeLengthMultiplier       (default 1.5x)
//   - SpikeWidth    × OverchargeWidthMultiplier        (default 1.5x)
//   After applying overcharge, ConsumeOvercharge() is called on the passive —
//   stacks reset, flag clears.
//
// DAMAGE TICK:
//   OnTriggerStay fires every physics step. A per-enemy cooldown dictionary
//   (_tickTimers) ensures each enemy takes damage at most once per
//   DAMAGE_TICK_INTERVAL seconds, not every physics frame.
//   The dictionary is cleared when the spike zone deactivates.
//
// SLOW APPLICATION:
//   Applied as a timed Sc_Modifier via ApplyToEnemy() each time an enemy takes
//   a damage tick. Re-applying refreshes the duration naturally because
//   Mb_StatBlock.AddModifier() starts a new removal coroutine each time.
//   TODO: If stacking slows becomes a problem, add a per-enemy HashSet to
//         prevent re-applying before the previous slow expires.
//
// VISUAL COMPONENTS:
//   The spike zone GO has two visual layers:
//   1. A child mesh (e.g. a flat tiled spike mesh or plane) that scales with
//      the box collider dimensions — assigned as SpikeVisualRoot in Inspector
//      on the prefab. Leo scales this to match collider size on the prefab.
//   2. "MindspikesCastVFX" — a burst particle system on Mari's prefab that plays
//      at cast time (e.g. ground crack / energy surge emanating from Mari).
//   3. "MindspikesFieldVFX" — a looping particle system on the spike zone prefab
//      itself that plays while the field is active (e.g. crackling psychic crystals).
//      This stops automatically when the zone deactivates.
//
// PREFAB SETUP (for Leo/Angel):
//   Create a prefab with:
//   - A BoxCollider (isTrigger = true) — sized to (SpikeWidth, SpikeHeight, SpikeLength)
//   - A child mesh GO for the visual spike field (flat plane with spike material/shader)
//   - A child ParticleSystem named "MindspikesFieldVFX" (looping crackling effect)
//   - Attach Mb_MindspikesZone (the inner trigger handler class below) to the root
//   Assign this prefab to Mari_Q.SpikeZonePrefab in the Inspector on Mari's prefab.
//
// INSPECTOR SETUP (on Mari_Q SO or Mari prefab — assigned via Mari_Q fields):
//   SpikeZonePrefab           — the rectangle trigger GO prefab (see above)
//   SpikeLength               — forward extent in world units     (default 6f)
//   SpikeWidth                — lateral extent in world units     (default 2.5f)
//   SpikeHeight               — vertical extent in world units    (default 2f)
//   SpikeDuration             — how long the field lasts          (default 3f)
//   DamageTickInterval        — seconds between damage ticks      (default 0.5f)
//   SlowPercent               — movement speed reduction %        (default 30f)
//   SlowDuration              — how long the slow lasts           (default 1.5f)
//   OverchargeDamageMultiplier — damage multiplier when overcharged (default 2f)
//   OverchargeSlowMultiplier  — slow % multiplier when overcharged  (default 2f)
//   OverchargeLengthMultiplier — length multiplier when overcharged (default 1.5f)
//   OverchargeWidthMultiplier  — width multiplier when overcharged  (default 1.5f)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mari_Q : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Inspector-Assigned Fields
    // These are set on the ability instance in Mari's AssignAbilities() or via
    // a wrapper SO. For prototype, sensible defaults are provided here.
    // -------------------------------------------------------------------------

    // The prefab for the spike field zone GO.
    // Must have: BoxCollider (isTrigger), Mb_MindspikesZone component,
    // child mesh for visuals, child ParticleSystem named "MindspikesFieldVFX".
    // TODO: Assign in Mari's prefab Inspector once Leo builds the asset.
    private GameObject _spikeZonePrefab;

    [Header("Zone Dimensions")]
    // TODO: Tune these once the visual asset exists and feel is established.
    [SerializeField] private float _SpikeLength = 6f;   // Forward extent
    [SerializeField] private float _SpikeWidth = 2.5f; // Lateral extent
    [SerializeField] private float _SpikeHeight = 2f;   // Fixed vertical extent

    [Header("Timing")]
    [SerializeField] private float _SpikeDuration = 3f;   // Field lifetime (seconds)
    [SerializeField] private float _DamageTickInterval = 0.5f; // Seconds between damage ticks

    [Header("Slow")]
    //[SerializeField] private float _SlowPercent = 30f;  // Movement speed reduction %
    [SerializeField] private float _SlowDuration = 1.0f; // Duration of each slow application

    [Header("Overcharge Multipliers")]
    [SerializeField] private float _OverchargeDamageMultiplier = 2f;
    [SerializeField] private float _OverchargeSlowMultiplier = 2f;
    [SerializeField] private float _OverchargeLengthMultiplier = 1.5f;
    [SerializeField] private float _OverchargeWidthMultiplier = 1.5f;


    // -------------------------------------------------------------------------
    // Cached References
    // -------------------------------------------------------------------------

    // Cast burst VFX — located by name in OnEquip
    private ParticleSystem _castVFX;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Mari_Q(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _spikeZonePrefab = registry?.GetPrefab(AbilityPrefabID.Mari_MindspikesZone);

        if (_spikeZonePrefab == null)
            Debug.LogError("[Mari_Q] Mari_MindspikesZone prefab not found in registry.");

        // Locate cast VFX by name in Mari's prefab hierarchy
        foreach (ParticleSystem ps in user.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "MindspikesCastVFX")
            {
                _castVFX = ps;
                break;
            }
        }

        Debug.Log($"[Mari_Q] Equipped {_AbilityData.AbilityName}.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[Mari_Q] Unequipped {_AbilityData.AbilityName}.");
    }


    // -------------------------------------------------------------------------
    // Activation
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        if (_spikeZonePrefab == null)
        {
            Debug.LogError("[Mari_Q] SpikeZonePrefab is not assigned. " +
                           "Assign the spike zone prefab in the Inspector.");
            return;
        }

        // --- Resolve overcharge state before consuming it ---
        bool isOvercharged = false;
        Mari_Passive passive = GetPassive();
        if (passive != null && passive.IsOvercharged)
        {
            isOvercharged = true;
            passive.ConsumeOvercharge();
            Debug.Log("[Mari_Q] Overcharged Mindspikes activated!");
        }

        // --- Resolve final dimensions ---
        float finalLength = isOvercharged
            ? _SpikeLength * _OverchargeLengthMultiplier
            : _SpikeLength;

        float finalWidth = isOvercharged
            ? _SpikeWidth * _OverchargeWidthMultiplier
            : _SpikeWidth;

        // --- Resolve final damage and slow ---
        float damageOverTime = _AbilityData.GetStat(
            "DoT",
            CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );
        float finalDamage = isOvercharged
            ? damageOverTime * _OverchargeDamageMultiplier
            : damageOverTime;

        float slowValue = _AbilityData.GetStat(
            "Slow",
            CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );
        float finalSlowPercent = isOvercharged
            ? slowValue * _OverchargeSlowMultiplier
            : slowValue;

        // --- Spawn position ---
        // Back edge of the rectangle starts at Mari's feet.
        // Center the GO at (SpikeLength / 2) ahead so the BoxCollider
        // extends from Mari's position forward to (SpikeLength) ahead.
        Vector3 spawnPos = user.transform.position
                         + user.transform.forward * (finalLength / 2f);

        // Rotate the GO to match Mari's facing direction
        Quaternion spawnRot = user.transform.rotation;

        // --- Instantiate spike zone ---
        GameObject zoneGO = GameObject.Instantiate(
            _spikeZonePrefab,
            spawnPos,
            spawnRot
        );

        // --- Configure the zone via its handler component ---
        Mb_MindspikesZone zone = zoneGO.GetComponent<Mb_MindspikesZone>();
        if (zone == null)
        {
            Debug.LogError("[Mari_Q] SpikeZonePrefab is missing Mb_MindspikesZone component.");
            GameObject.Destroy(zoneGO);
            return;
        }

        zone.Initialize(
            owner: user,
            damage: finalDamage,
            tickInterval: _DamageTickInterval,
            slowPercent: finalSlowPercent,
            slowDuration: _SlowDuration,
            length: finalLength,
            width: finalWidth,
            height: _SpikeHeight,
            duration: _SpikeDuration,
            isOvercharged: isOvercharged
        );

        // --- Cast VFX ---
        _castVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _castVFX?.Play();

        TriggerAbilityAnimation(user);

        StartCooldown(user, GetAbilityCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Passive Retrieval
    // -------------------------------------------------------------------------

    // Fetches Mari_Passive from the ability controller without storing a
    // persistent reference — the passive slot could theoretically change,
    // so we read it fresh each activation.
    private Mari_Passive GetPassive()
    {
        Sc_BaseAbility passive = _User.Abilities.GetAbilityBySlot(AbilitySlot.Passive);
        return passive as Mari_Passive;
    }


    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        // TODO: Add TriggerQAbility() call once Angel rigs Mari's Q animation.
        // For now, reuse the existing Q trigger so it doesn't silently fail.
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerQAbility();
    }
}

