// Mari_Q.cs
// [Q] Mindspikes — Mari erupts psychic spikes as ground-projected tiles in front of her.
//
// BEHAVIOR:
//   On cast, Mari samples a rectangle in front of her, raycasts each sample down
//   onto valid ground, and spawns one small trigger tile per successful ground hit.
//   Each tile follows its local ground normal, so cliffs and steep terrain do not
//   get bridged by one large flat collider. Enemies inside any tile are damaged
//   every DAMAGE_TICK_INTERVAL seconds and slowed for SLOW_DURATION seconds.
//
// TILE DIMENSIONS:
//   Tile length/width are target sizes. The final tile count is resolved from
//   the total field size, then each tile is resized so the projected grid fills
//   the full final length and width evenly.
//   Tile height is intentionally short and starts at the local ground surface.
//   Tiles are skipped when no ground is found or the surface is too steep.
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
//   All tiles from the same cast share the same tick dictionary so standing on
//   tile seams does not multiply damage.
//
// SLOW APPLICATION:
//   Applied as a timed Sc_Modifier via ApplyToEnemy() each time an enemy takes
//   a damage tick. Re-applying refreshes the duration naturally because
//   Mb_StatBlock.AddModifier() starts a new removal coroutine each time.
//   TODO: If stacking slows becomes a problem, add a per-enemy HashSet to
//         prevent re-applying before the previous slow expires.
//
// VISUAL COMPONENTS:
//   Each tile GO has two visual layers:
//   1. A child mesh (e.g. a flat spike patch or plane) that scales with the
//      tile collider dimensions.
//   2. "MindspikesCastVFX" — a burst particle system on Mari's prefab that plays
//      at cast time (e.g. ground crack / energy surge emanating from Mari).
//   3. "MindspikesFieldVFX" — a looping particle system on the spike zone prefab
//      itself that plays while the field is active (e.g. crackling psychic crystals).
//      This stops automatically when the zone deactivates.
//
// PREFAB SETUP (for Leo/Angel):
//   Create a prefab with:
//   - A BoxCollider (isTrigger = true) — one small tile, resized at runtime
//   - A Rigidbody (isKinematic = true, useGravity = false)
//   - A child mesh GO for the visual spike tile (flat plane with spike material/shader)
//   - A child ParticleSystem named "MindspikesFieldVFX" (looping crackling effect)
//   - Attach Mb_MindspikesZone (the inner trigger handler class below) to the root
//   Assign this prefab to the Mari_MindspikesZone slot in Mb_AbilityPrefabRegistry.
//
// INSPECTOR SETUP (on Mari_Q SO or Mari prefab — assigned via Mari_Q fields):
//   SpikeZonePrefab           — the rectangle trigger GO prefab (see above)
//   SpikeLength               — total forward extent in world units
//   SpikeWidth                — total lateral extent in world units
//   SpikeHeight               — per-tile trigger height above local ground
//   TileLength                — target forward size of each spawned tile
//   TileWidth                 — target lateral size of each spawned tile
//   Min/MaxLengthTiles        — lower/upper bound for forward tile count
//   Min/MaxWidthTiles         — lower/upper bound for lateral tile count
//   SpikeDuration             — how long the field lasts          (default 3f)
//   DamageTickInterval        — seconds between damage ticks      (default 0.5f)
//   SlowPercent               — movement speed reduction %        (default 30f)
//   SlowDuration              — how long the slow lasts           (default 1.5f)
//   OverchargeDamageMultiplier — damage multiplier when overcharged (default 2f)
//   OverchargeSlowMultiplier  — slow % multiplier when overcharged  (default 2f)
//   OverchargeLengthMultiplier — length multiplier when overcharged (default 1.5f)
//   OverchargeWidthMultiplier  — width multiplier when overcharged  (default 1.5f)

using System.Collections.Generic;
using UnityEngine;

public class Mari_Q : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Inspector-Assigned Fields
    // These are set on the ability instance in Mari's AssignAbilities() or via
    // a wrapper SO. For prototype, sensible defaults are provided here.
    // -------------------------------------------------------------------------

    // The prefab for one spike field tile GO.
    // Must have: BoxCollider (isTrigger), Mb_MindspikesZone component,
    // child mesh for visuals, child ParticleSystem named "MindspikesFieldVFX".
    private GameObject _spikeZonePrefab;

    [Header("Zone Dimensions")]
    [SerializeField] private float _SpikeLength = 18;   // Forward extent
    [SerializeField] private float _SpikeWidth = 4.0f; // Lateral extent
    [SerializeField] private float _SpikeHeight = 1.75f;   // Per-tile vertical extent

    [Header("Tile Projection")]
    [SerializeField] private float _TileLength = 1.5f;
    [SerializeField] private float _TileWidth = 1.5f;
    [SerializeField] private int _MinLengthTiles = 4;
    [SerializeField] private int _MaxLengthTiles = 16;
    [SerializeField] private int _MinWidthTiles = 2;
    [SerializeField] private int _MaxWidthTiles = 6;
    [SerializeField] private float _GroundProbeHeight = 8f;
    [SerializeField] private float _GroundProbeDistance = 24f;
    [SerializeField] private float _GroundSurfaceOffset = 0.03f;
    [SerializeField] private float _MaxGroundAngle = 65f;

    [Header("Timing")]
    [SerializeField] private float _SpikeDuration = 5f;   // Field lifetime (seconds)
    [SerializeField] private float _DamageTickInterval = 0.5f; // Seconds between damage ticks

    [Header("Slow")]
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

        int spawnedTileCount = SpawnGroundTiles(
            user,
            finalLength,
            finalWidth,
            finalDamage,
            finalSlowPercent,
            isOvercharged
        );

        if (spawnedTileCount == 0)
        {
            Debug.LogWarning("[Mari_Q] Mindspikes found no valid ground tiles.");
            return;
        }

        // --- Cast VFX ---
        _castVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _castVFX?.Play();

        TriggerAbilityAnimation(user);

        StartCooldown(user, GetAbilityCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Ground Tile Projection
    // -------------------------------------------------------------------------

    private int SpawnGroundTiles(
        Mb_CharacterBase user,
        float finalLength,
        float finalWidth,
        float finalDamage,
        float finalSlowPercent,
        bool isOvercharged)
    {
        Vector3 forward = user.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.001f)
            forward = user.transform.forward;
        forward.Normalize();

        Vector3 right = user.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude <= 0.001f)
            right = Vector3.Cross(Vector3.up, forward);
        right.Normalize();

        int lengthSteps = ResolveTileCount(
            finalLength,
            _TileLength,
            _MinLengthTiles,
            _MaxLengthTiles
        );

        int widthSteps = ResolveTileCount(
            finalWidth,
            _TileWidth,
            _MinWidthTiles,
            _MaxWidthTiles
        );

        float tileLength = finalLength / lengthSteps;
        float tileWidth = finalWidth / widthSteps;
        float halfWidth = finalWidth * 0.5f;

        Sc_MindspikesTickTracker sharedTickTracker = new Sc_MindspikesTickTracker();
        int spawnedTileCount = 0;
        int groundMask = GetGroundProjectionMask();

        for (int z = 0; z < lengthSteps; z++)
        {
            float forwardOffset = (z + 0.5f) * tileLength;

            for (int x = 0; x < widthSteps; x++)
            {
                float lateralOffset = -halfWidth + ((x + 0.5f) * tileWidth);
                Vector3 sampleCenter = user.transform.position
                                     + forward * forwardOffset
                                     + right * lateralOffset;

                if (!TryProjectTile(sampleCenter, forward, groundMask, out Vector3 tilePosition, out Quaternion tileRotation))
                    continue;

                GameObject tileGO = GameObject.Instantiate(
                    _spikeZonePrefab,
                    tilePosition,
                    tileRotation
                );

                Mb_MindspikesZone zone = tileGO.GetComponent<Mb_MindspikesZone>();
                if (zone == null)
                {
                    Debug.LogError("[Mari_Q] Spike tile prefab is missing Mb_MindspikesZone component.");
                    GameObject.Destroy(tileGO);
                    continue;
                }

                zone.Initialize(
                    owner: user,
                    damage: finalDamage,
                    tickInterval: _DamageTickInterval,
                    slowPercent: finalSlowPercent,
                    slowDuration: _SlowDuration,
                    length: tileLength,
                    width: tileWidth,
                    height: _SpikeHeight,
                    duration: _SpikeDuration,
                    isOvercharged: isOvercharged,
                    sharedTickTracker: sharedTickTracker
                );

                spawnedTileCount++;
            }
        }

        return spawnedTileCount;
    }


    private int ResolveTileCount(float totalSize, float targetTileSize, int minTiles, int maxTiles)
    {
        float safeTotalSize = Mathf.Max(0.1f, totalSize);
        float safeTargetTileSize = Mathf.Max(0.1f, targetTileSize);
        int safeMinTiles = Mathf.Max(1, minTiles);
        int safeMaxTiles = Mathf.Max(safeMinTiles, maxTiles);

        int resolvedCount = Mathf.CeilToInt(safeTotalSize / safeTargetTileSize);
        return Mathf.Clamp(resolvedCount, safeMinTiles, safeMaxTiles);
    }


    private bool TryProjectTile(
        Vector3 sampleCenter,
        Vector3 fieldForward,
        int groundMask,
        out Vector3 tilePosition,
        out Quaternion tileRotation)
    {
        Vector3 rayOrigin = sampleCenter + Vector3.up * _GroundProbeHeight;

        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                _GroundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            tilePosition = Vector3.zero;
            tileRotation = Quaternion.identity;
            return false;
        }

        float groundAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (groundAngle > _MaxGroundAngle)
        {
            tilePosition = Vector3.zero;
            tileRotation = Quaternion.identity;
            return false;
        }

        Vector3 projectedForward = Vector3.ProjectOnPlane(fieldForward, hit.normal);
        if (projectedForward.sqrMagnitude <= 0.001f)
            projectedForward = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);

        projectedForward.Normalize();

        tilePosition = hit.point + hit.normal * _GroundSurfaceOffset;
        tileRotation = Quaternion.LookRotation(projectedForward, hit.normal);
        return true;
    }


    private int GetGroundProjectionMask()
    {
        int mask = Physics.DefaultRaycastLayers;
        ExcludeLayer(ref mask, "Character");
        ExcludeLayer(ref mask, "Player");
        ExcludeLayer(ref mask, "CuBot");
        ExcludeLayer(ref mask, "Panoharra");
        ExcludeLayer(ref mask, "Ignore Raycast");
        return mask;
    }


    private void ExcludeLayer(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
            mask &= ~(1 << layer);
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

