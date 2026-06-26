// Mb_VFXManager.cs
// Central VFX playback manager for Forest Guardians.
// Create one instance in Bootstrap and keep it alive across scene loads.
//
// OBJECT POOLING:
//   Combat can spawn many simultaneous hit effects (multi-enemy abilities, DoT ticks,
//   CuBot death bursts). Instantiating a new GameObject per effect would generate
//   garbage and cause visible stutter during the most visually active moments.
//   Pre-warming a pool per VFXType at scene start means zero runtime allocation
//   during gameplay — the same pattern used by Mb_AudioManager's SFX pool.
//
// STATIC API:
//   Mb_VFXManager.Play(VFXType.Hit_Generic, hitPosition);
//   Mb_VFXManager.Play(VFXType.Status_Burn, transform.position, transform);
//   Mb_VFXManager.PlayAtImpact(VFXType.Hit_Critical, hitPoint, hitNormal);
//   Mb_VFXManager.Stop(VFXType.Status_Burn, targetGameObject);
//
// POOL EXHAUSTION STRATEGY:
//   If a pool runs out, one new instance is allocated, a warning is logged, and
//   the instance is used normally. The warning flags which VFXType needs a larger
//   pool size in SO_VFXLibrary. Silent failure (skipping the effect) was rejected
//   because missing VFX during combat are harder to diagnose than a console warning.
//
// EVENT HOOKS:
//   MB_CuBotBase.OnCuBotSpawn     → intentionally skipped here; spawn position is set after activation
//   MB_CuBotBase.OnCuBotDeath     → Hit_CuBot_Death
//   Sc_BaseAbility.OnCriticalHit  → Hit_Critical
//   Mb_WaveManager.OnWaveStart    → Env_Wave_Start  (at Panoharra position)
//
// ABILITY-SPECIFIC VFX:
//   Call Mb_VFXManager.Play() directly inside the ability's Activate() method.
//
// PROJECTILE IMPACT VFX:
//   Call PlayAtImpact() from Mb_Projectile.OnTriggerEnter() when the impact point
//   is known. Fall back to the target's transform.position when it is not.
//
// INSPECTOR SETUP:
//   1. Create a persistent GameObject in your bootstrap scene (e.g. "VFXManager").
//   2. Attach this script.
//   3. Assign the SO_VFXLibrary asset to the VFX Library field.
//   4. The pool root is created automatically at runtime — no scene setup needed.

using System.Collections.Generic;
using UnityEngine;

public class Mb_VFXManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static Mb_VFXManager Instance { get; private set; }


    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("VFX Library")]
    [Tooltip("Assign the SO_VFXLibrary asset here. " +
             "All prefab references and pool sizes are read from this asset.")]
    [SerializeField] private SO_VFXLibrary _VFXLibrary;


    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    // One pool stack per VFXType — available (inactive) instances sit here.
    // Stack is LIFO so the most-recently-returned instance is reused first,
    // keeping recently-used objects warm in CPU cache.
    private Dictionary<VFXType, Stack<Mb_VFXInstance>> _availablePools
        = new Dictionary<VFXType, Stack<Mb_VFXInstance>>();

    // All instances ever created, keyed by VFXType — needed to pause/resume all
    // active instances on game pause without iterating scene objects.
    private Dictionary<VFXType, List<Mb_VFXInstance>> _allInstances
        = new Dictionary<VFXType, List<Mb_VFXInstance>>();

    // Per-target tracking for Stop(VFXType, GameObject) lookups.
    // Maps a target GameObject to the VFXInstance currently playing on it.
    // Used by Mb_StatusVFXHandler to stop a specific instance on a specific character.
    // Key: (VFXType, target GameObject)  Value: the active Mb_VFXInstance
    private Dictionary<(VFXType, GameObject), Mb_VFXInstance> _activeOnTarget
        = new Dictionary<(VFXType, GameObject), Mb_VFXInstance>();

    // Parent transform that holds all pooled VFX GameObjects in the hierarchy.
    // Keeps the scene tidy — all inactive instances live under "VFXPool" in the hierarchy.
    private Transform _poolRoot;

    // Cached Panoharra transform — resolved lazily when OnWaveStart fires.
    // Null until first wave start; re-resolved each wave in case the scene changed.
    private Transform _panoharraTransform;


    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Standard singleton guard — only one VFXManager should ever exist
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create a child GameObject to parent all pooled instances under —
        // keeps the hierarchy readable during development
        GameObject poolRootGO = new GameObject("VFXPool");
        poolRootGO.transform.SetParent(transform);
        _poolRoot = poolRootGO.transform;

        // Validate the library before building pools — logs warnings for
        // missing prefabs so the team catches gaps immediately
        if (_VFXLibrary == null)
        {
            Debug.LogError("[Mb_VFXManager] No SO_VFXLibrary assigned! " +
                           "Drag the VFXLibrary asset into the Inspector field.");
            return;
        }

        _VFXLibrary.Validate();
        WarmUpAllPools();
    }


    private void OnEnable()
    {
        // Subscribe to game events that trigger automatic VFX responses.
        // All subscriptions here so unsubscribing in OnDisable is symmetric.
        MB_CuBotBase.OnCuBotSpawn += HandleCuBotSpawn;
        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath;
        Sc_BaseAbility.OnCriticalHit += HandleCriticalHit;
        Mb_WaveManager.OnWaveStart += HandleWaveStart;
        Mb_PauseManager.OnPaused += HandlePaused;
        Mb_PauseManager.OnResumed += HandleResumed;
        Mb_Projectile.OnAnyProjectileHit += HandleProjectileHit;
    }


    private void OnDisable()
    {
        MB_CuBotBase.OnCuBotSpawn -= HandleCuBotSpawn;
        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;
        Sc_BaseAbility.OnCriticalHit -= HandleCriticalHit;
        Mb_WaveManager.OnWaveStart -= HandleWaveStart;
        Mb_PauseManager.OnPaused -= HandlePaused;
        Mb_PauseManager.OnResumed -= HandleResumed;
        Mb_Projectile.OnAnyProjectileHit -= HandleProjectileHit;

    }


    // ─────────────────────────────────────────────────────────────────────────
    // Static Public API
    // All external callers use these — never reach into the instance directly.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a VFX at the given world position with default upward rotation.
    /// Use for effects that don't need a specific orientation (e.g. level-up, spawn).
    /// </summary>
    public static void Play(VFXType type, Vector3 position)
    {
        if (Instance == null) return;
        Instance.PlayInternal(type, position, Quaternion.identity, null);
    }


    /// <summary>
    /// Spawns a VFX at the given world position with an explicit rotation.
    /// Use when the effect needs a specific facing direction (e.g. projectile trail).
    /// </summary>
    public static void Play(VFXType type, Vector3 position, Quaternion rotation)
    {
        if (Instance == null) return;
        Instance.PlayInternal(type, position, rotation, null);
    }


    /// <summary>
    /// Spawns a VFX at the given position and parents it to a Transform.
    /// Use for status effects that must follow a character as they move.
    /// The VFX will automatically unparent when it stops or is returned to pool.
    /// </summary>
    public static void Play(VFXType type, Vector3 position, Transform parent)
    {
        if (Instance == null) return;
        Instance.PlayInternal(type, position, Quaternion.identity, parent);
    }


    /// <summary>
    /// Spawns a VFX oriented so its forward axis aligns with the surface normal.
    /// Use for hit sparks and impact effects so they read correctly on
    /// surfaces of any angle (walls, floors, angled terrain).
    ///
    /// hitPoint:  the world position where the impact occurred (e.g. collision contact point)
    /// hitNormal: the surface normal at the hit point (e.g. collision.contacts[0].normal)
    /// </summary>
    public static void PlayAtImpact(VFXType type, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (Instance == null) return;

        // Orient the VFX so it faces away from the surface.
        // LookRotation(hitNormal) points the forward axis along the normal —
        // particles will emit away from the surface rather than into it.
        // Guard against a zero normal (shouldn't happen in physics hits, but safe).
        Quaternion rotation = hitNormal != Vector3.zero
            ? Quaternion.LookRotation(hitNormal)
            : Quaternion.identity;

        Instance.PlayInternal(type, hitPoint, rotation, null);
    }


    /// <summary>
    /// Stops and returns the VFX instance that is currently playing on a specific target.
    /// Use this when a status effect ends — pass the character's GameObject as the target.
    /// Safe to call even if no VFX of that type is active on the target.
    /// </summary>
    public static void Stop(VFXType type, GameObject target)
    {
        if (Instance == null) return;
        Instance.StopInternal(type, target);
    }


    /// <summary>
    /// Called by Mb_VFXInstance when its lifetime expires or Stop() is called.
    /// Returns the instance to the correct pool so it can be reused.
    /// Static so Mb_VFXInstance can call it without a direct manager reference.
    /// </summary>
    public static void ReturnInstance(VFXType type, Mb_VFXInstance instance)
    {
        if (Instance == null) return;
        Instance.ReturnInstanceInternal(type, instance);
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Internal Play / Stop / Return
    // ─────────────────────────────────────────────────────────────────────────

    private void PlayInternal(VFXType type, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (_VFXLibrary == null) return;

        // Look up the entry to get lifetime — needed by Mb_VFXInstance.Play()
        if (!_VFXLibrary.TryGetEntry(type, out VFXEntry entry))
        {
            Debug.LogWarning($"[Mb_VFXManager] Play called for VFXType.{type} " +
                             "but no entry exists in the library. Skipping.");
            return;
        }

        if (entry.Prefab == null)
        {
            Debug.LogWarning($"[Mb_VFXManager] VFXType.{type} has no prefab assigned. Skipping.");
            return;
        }

        Mb_VFXInstance instance = FetchFromPool(type, entry);
        if (instance == null) return; // FetchFromPool logs its own error if something went wrong

        instance.Play(position, rotation, entry.Lifetime, parent);

        // If a parent was provided, register this instance as active on that target
        // so Stop(type, parent.gameObject) can find it later
        if (parent != null)
        {
            var key = (type, parent.gameObject);

            // If there is already an instance of this type on this target, stop the old one
            // first — avoids two looping status VFX of the same type stacking on one character
            if (_activeOnTarget.TryGetValue(key, out Mb_VFXInstance existing))
            {
                existing.Stop();
                _activeOnTarget.Remove(key);
            }

            _activeOnTarget[key] = instance;
        }
    }


    private void StopInternal(VFXType type, GameObject target)
    {
        var key = (type, target);

        if (!_activeOnTarget.TryGetValue(key, out Mb_VFXInstance instance))
            return; // No active instance of this type on this target — nothing to do

        _activeOnTarget.Remove(key);

        // Stop() on the instance triggers ReturnToPool internally,
        // which calls ReturnInstance() back here to push it onto the available stack
        instance.Stop();
    }


    private void ReturnInstanceInternal(VFXType type, Mb_VFXInstance instance)
    {
        // Re-parent to the pool root so it lives tidily under VFXPool in the hierarchy
        instance.transform.SetParent(_poolRoot);

        if (_availablePools.TryGetValue(type, out Stack<Mb_VFXInstance> pool))
            pool.Push(instance);

        // Also clean up any stale _activeOnTarget entries that reference this instance.
        // This handles the case where ReturnToPool was called by the lifetime timer
        // (not by StopInternal), so the _activeOnTarget entry was never removed.
        CleanStaleTargetEntries(instance);
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Pool Management
    // ─────────────────────────────────────────────────────────────────────────

    // Pre-warms all pools by instantiating PoolSize instances per VFXType.
    // Called once in Awake() — no Instantiate calls happen after this during gameplay.
    private void WarmUpAllPools()
    {
        foreach (VFXType type in System.Enum.GetValues(typeof(VFXType)))
        {
            if (!_VFXLibrary.TryGetEntry(type, out VFXEntry entry)) continue;
            if (entry.Prefab == null) continue;

            _availablePools[type] = new Stack<Mb_VFXInstance>();
            _allInstances[type] = new List<Mb_VFXInstance>();

            for (int i = 0; i < entry.PoolSize; i++)
                CreateNewInstance(type, entry);
        }

        Debug.Log("[Mb_VFXManager] All VFX pools warmed up.");
    }


    // Fetches an available instance from the pool for the given type.
    // If the pool is exhausted, creates a new instance and logs a warning.
    private Mb_VFXInstance FetchFromPool(VFXType type, VFXEntry entry)
    {
        if (!_availablePools.TryGetValue(type, out Stack<Mb_VFXInstance> pool))
        {
            // This type has no pool — shouldn't happen if WarmUpAllPools ran, but guard anyway
            Debug.LogError($"[Mb_VFXManager] No pool exists for VFXType.{type}. " +
                           "Was WarmUpAllPools() called?");
            return null;
        }

        if (pool.Count > 0)
            return pool.Pop();

        // Pool exhausted — expand it rather than silently skipping the effect.
        // Log a warning so the team knows to increase PoolSize in SO_VFXLibrary.
        Debug.LogWarning($"[Mb_VFXManager] Pool exhausted for VFXType.{type}. " +
                         $"Allocating a new instance. Consider increasing PoolSize " +
                         $"for this entry in SO_VFXLibrary.");

        return CreateNewInstance(type, entry);
    }


    // Instantiates one VFX instance, initializes it, and places it in the pool root.
    // Returns the instance so FetchFromPool can use it immediately if the pool was exhausted.
    private Mb_VFXInstance CreateNewInstance(VFXType type, VFXEntry entry)
    {
        GameObject go = Instantiate(entry.Prefab, _poolRoot);
        go.SetActive(false);

        Mb_VFXInstance instance = go.GetComponent<Mb_VFXInstance>();

        if (instance == null)
        {
            // The prefab is missing its Mb_VFXInstance component — can't use it
            Debug.LogError($"[Mb_VFXManager] Prefab for VFXType.{type} is missing an " +
                           "Mb_VFXInstance component. Add it to the prefab.");
            Destroy(go);
            return null;
        }

        instance.Initialize(type);

        // Track in _allInstances so we can iterate all instances during pause/resume
        _allInstances[type].Add(instance);

        // Push onto the available pool — it will be popped when next needed
        _availablePools[type].Push(instance);

        return instance;
    }


    // Scans _activeOnTarget for any entry pointing to the given instance and removes it.
    // Called after a lifetime-timer return so the dictionary stays clean.
    private void CleanStaleTargetEntries(Mb_VFXInstance instance)
    {
        // Build a removal list first — never modify a dictionary while iterating it
        var keysToRemove = new List<(VFXType, GameObject)>();

        foreach (var kvp in _activeOnTarget)
        {
            if (kvp.Value == instance)
                keysToRemove.Add(kvp.Key);
        }

        foreach (var key in keysToRemove)
            _activeOnTarget.Remove(key);
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers — Automatic VFX Responses
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleCuBotSpawn(GameObject cuBotObj)
    {
        // OnCuBotSpawn does not provide a position — the CuBot fires this event
        // in OnEnable before its position is set by the WaveManager spawn logic.
        // We skip position-dependent VFX here to avoid a flash at the wrong location.
        // TODO: Move spawn VFX to after the CuBot's position is set —
        //       either by adding a position parameter to OnCuBotSpawn, or by
        //       having MB_CuBotBase fire a delayed event after SetActive().
        //       For now, VFX is omitted rather than played at the wrong position.
    }


    private void HandleCuBotDeath(GameObject deadCuBot)
    {
        if (deadCuBot == null) return;

        // Boss CuBots use a heavier death burst than standard enemies.
        if (deadCuBot.name.Contains("Bernie") || deadCuBot.name.Contains("Toxion") || deadCuBot.name.Contains("Luxion"))
        {
            Play(VFXType.CuBot_Boss_Death_Generic, deadCuBot.transform.position);
        }
        else
        {
            Play(VFXType.CuBot_Death_Generic, deadCuBot.transform.position);
        }
    }


    private void HandleCriticalHit(float critDamage, Mb_CharacterBase attacker)
    {
        if (attacker == null) return;

        // Play the crit flash at the attacker's position.
        // TODO: Pass the actual impact point once it is available from the ability —
        //       currently no standard way to get it from this event signature.
        //       For projectile crits, PlayAtImpact() should be called inside
        //       Mb_Projectile.OnTriggerEnter() directly instead.
        Play(VFXType.Hit_Critical, attacker.transform.position);
    }

    private void HandleProjectileHit(Mb_Projectile projectile, Mb_CharacterBase attacker, Mb_CharacterBase characterHit)
    {
        Transform projectileTrans = projectile.transform;

        PlayAtImpact(VFXType.Hit_Projectile_Generic, projectileTrans.position, Vector3.up);
    }


    private void HandleWaveStart(int waveIndex)
    {
        // Resolve the Panoharra position lazily — once per wave start.
        // We use FindGameObjectWithTag here because the manager is DontDestroyOnLoad
        // and cannot hold a scene-specific reference safely from Awake.
        // This runs at most once per wave (not per frame), so the cost is acceptable.
        if (_panoharraTransform == null)
        {
            GameObject panoharra = GameObject.FindGameObjectWithTag("Panoharra");

            if (panoharra != null)
                _panoharraTransform = panoharra.transform;
            else
                Debug.LogWarning("[Mb_VFXManager] HandleWaveStart: No GameObject with tag " +
                                 "'Panoharra' found. Wave_Start VFX will play at world origin.");
        }

        Vector3 spawnPos = _panoharraTransform != null
            ? _panoharraTransform.position
            : Vector3.zero;

        Play(VFXType.Wave_Start, spawnPos);
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Pause Handling
    // ─────────────────────────────────────────────────────────────────────────

    private void HandlePaused()
    {
        // Active VFX instances handle pause through their own event subscriptions.
    }


    private void HandleResumed()
    {
        // Same reasoning as HandlePaused — active instances self-resume via their
        // own Mb_PauseManager.OnResumed subscription. No additional action needed here.
    }
}
