// Mb_StatusEffectController.cs
// Manages all status effects on a character — both Guardians and CuBots.
// Sits as a sibling component alongside Mb_StatBlock and Mb_HealthComponent.
//
// RESPONSIBILITIES:
//   - Apply status effects and their stat modifiers via Mb_StatBlock
//   - Run DoT tick coroutines that pause with the game
//   - Enforce the one-effect-per-StatusType rule unless an effect is explicitly stackable
//   - Remove effects cleanly when they expire or are force-cleared
//   - Self-clear on OnEnable() so CuBot pool reuse starts with a clean slate
//
// POOL SAFETY NOTE:
//   OnEnable() calls ClearAll() so this component handles its own reset on
//   CuBot pool reuse — no changes to MB_CuBotBase are needed.
//   MB_CuBotBase.Reset() calls Stats.RemoveAllModifiers() which already clears
//   stat effects, but ClearAll() also stops coroutines and clears the dictionary,
//   which RemoveAllModifiers() does not do.
//
// HOW TO APPLY A STATUS EFFECT FROM AN ABILITY SCRIPT:
//   1. Get the controller: var status = target.GetComponent<Mb_StatusEffectController>();
//   2. Build the effect:   var slow = Sc_StatusEffect.MoveSlow(3f, 0.30f);
//   3. Apply it:           status?.Apply(slow);
//
// Inspector setup:
//   Add this component to any character GameObject that can receive status effects.
//   No fields need to be assigned — all references are fetched in Awake().

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mb_StatusEffectController : MonoBehaviour
{

    #region Private State           //----------------------------------------

    // The stat system on this character — used to apply and remove modifiers
    private Mb_StatBlock _statBlock;

    // The health system on this character — used by DoT ticks to deal damage
    private Mb_HealthComponent _health;

    // Tracks every currently active effect by type.
    // Only one entry per StatusType can exist at a time.
    // Value is the remaining duration — updated by each effect's coroutine.
    private readonly Dictionary<StatusType, float> _activeEffects
        = new Dictionary<StatusType, float>();

    // Tracks the running coroutine for each active non-stacking effect so we can stop it
    // on reapplication or forced removal without needing to find it by reference.
    private readonly Dictionary<StatusType, Coroutine> _activeCoroutines
        = new Dictionary<StatusType, Coroutine>();

    // Tracks which modifier we applied for each effect so we can remove
    // exactly that modifier later — not all StatusEffect modifiers, just this one.
    private readonly Dictionary<StatusType, Sc_Modifier> _appliedModifiers
        = new Dictionary<StatusType, Sc_Modifier>();

    // Stackable effects use unique ids so each application can run its own timer
    // and DoT ticks without interfering with other instances of the same StatusType.
    private readonly Dictionary<int, StatusType> _stackedEffectTypes
        = new Dictionary<int, StatusType>();

    private readonly Dictionary<int, Coroutine> _stackedEffectCoroutines
        = new Dictionary<int, Coroutine>();

    private readonly Dictionary<int, Sc_Modifier> _stackedEffectModifiers
        = new Dictionary<int, Sc_Modifier>();

    private int _nextStackedEffectId = 1;

    // Pause flag — set by Mb_PauseManager events so coroutines can yield cleanly.
    // Using a local flag instead of polling Mb_PauseManager.IsPaused directly
    // keeps the coroutines decoupled from the pause system's implementation.
    private bool _isPaused = false;

    #endregion                      //----------------------------------------


    #region Public Read-Only API    //----------------------------------------

    // Read-only snapshot of active effects and their remaining durations.
    // The HUD and Mb_FloatingTextSpawner can read this each frame without
    // the controller needing to know anything about those systems.
    // Remaining duration values are updated live by each effect's coroutine.
    public IReadOnlyDictionary<StatusType, float> ActiveStatuses => _activeEffects;

    #endregion                      //----------------------------------------


    #region Events                  //----------------------------------------

    // Fired when a new effect starts — pass StatusType so listeners only react
    // to effects they care about (e.g. CuBot AI reacting to Stun).
    public event Action<StatusType> OnStatusApplied;

    // Fired when an effect ends, whether by expiry or forced removal.
    public event Action<StatusType> OnStatusRemoved;

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        _statBlock = GetComponent<Mb_StatBlock>();
        _health = GetComponent<Mb_HealthComponent>();

        if (_statBlock == null)
            Debug.LogError($"[Mb_StatusEffectController] No Mb_StatBlock found on {gameObject.name}.");

        if (_health == null)
            Debug.LogError($"[Mb_StatusEffectController] No Mb_HealthComponent found on {gameObject.name}.");
    }


    private void OnEnable()
    {
        // Subscribe to pause events so DoT coroutines freeze with the game
        Mb_PauseManager.OnPaused += HandlePause;
        Mb_PauseManager.OnResumed += HandleResume;

        // Pool safety — clear any effects left over from a previous life.
        // On the very first enable Awake hasn't run yet IF OnEnable fires before Awake,
        // but Unity guarantees Awake fires before OnEnable on the same object,
        // so _statBlock is always valid here.
        ClearAll();
    }


    private void OnDisable()
    {
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;
    }

    #endregion                      //----------------------------------------


    #region Public API              //----------------------------------------

    /// <summary>
    /// Applies a status effect to this character.
    ///
    /// REAPPLICATION RULE:
    ///   If the same StatusType is already active, the duration is refreshed
    ///   and the status modifier is replaced unless the effect is stackable.
    ///   This keeps behavior predictable when abilities hit the same target twice.
    ///
    /// If the character is already dead, the effect is silently ignored.
    /// </summary>
    public void Apply(Sc_StatusEffect effect)
    {
        // Never apply effects to a dead character — DoT ticks would fire on a corpse
        if (_health != null && _health.IsDead) return;

        if (effect.CanStack)
        {
            ApplyStackableEffect(effect);
            return;
        }

        if (_activeEffects.ContainsKey(effect.Type))
        {
            // Same type is already running — refresh the duration and modifier.
            // The coroutine reads _activeEffects[type] each tick, so updating
            // the value here is enough to extend the effect without restarting anything.
            _activeEffects[effect.Type] = effect.Duration;
            ReplaceModifierForType(effect.Type, effect.StatModifier);

            Debug.Log($"[Mb_StatusEffectController] {effect.Type} refreshed on {gameObject.name} " +
                      $"({effect.Duration}s).");
            return;
        }

        // New effect — track it, apply its modifier, and start its coroutine
        _activeEffects[effect.Type] = effect.Duration;

        // Apply the stat modifier if this effect has one.
        // Stored by type so we can remove exactly this modifier later.
        if (effect.StatModifier != null && _statBlock != null)
        {
            _statBlock.AddModifier(effect.StatModifier);
            _appliedModifiers[effect.Type] = effect.StatModifier;
        }

        // Start the effect coroutine — handles both duration countdown and DoT ticks
        Coroutine routine = StartCoroutine(EffectRoutine(effect));
        _activeCoroutines[effect.Type] = routine;


        //// VFX
        //// Map StatusType to VFX enums
        //switch(effect.Type)
        //{
        //    //case StatusType.MoveSlow:
        //    //case StatusType.AttackSlow:
        //    //    VFXType = VFXType.Status_Slow;
        //    //    break;
        //    //case StatusType.Poison:
        //    //    VFXType = VFXType.Status_Poison;
        //    //    break;
        //    case StatusType.Burn:

        //        // Character VFX
        //        Mb_VFXManager.Play(VFXType.Status_Burn, transform.position, transform);

        //        // Camera VFX
        //        GameObject cam = GameObject.FindGameObjectWithTag("MainCamera");
        //        Mb_VFXManager.Play(VFXType.Burn_Vignette, cam.transform.position, cam.transform);

        //        break;
        //    //case StatusType.Stun:
        //    //    VFXType = VFXType.Status_Stun;
        //    //    break;
        //}

        OnStatusApplied?.Invoke(effect.Type);

        Debug.Log($"[Mb_StatusEffectController] {effect.Type} applied to {gameObject.name} " +
                  $"({effect.Duration}s).");
    }


    /// <summary>
    /// Forcibly removes a status effect before it expires.
    /// Safe to call even if the effect is not currently active — does nothing in that case.
    /// </summary>
    public void Remove(StatusType type)
    {
        bool removedAny = RemoveStackableEffectsOfType(type);

        if (!_activeEffects.ContainsKey(type))
        {
            if (removedAny)
                Debug.Log($"[Mb_StatusEffectController] {type} stacks forcibly removed from {gameObject.name}.");

            return;
        }

        StopEffectCoroutine(type);
        RemoveModifierForType(type);
        _activeEffects.Remove(type);

        OnStatusRemoved?.Invoke(type);

        Debug.Log($"[Mb_StatusEffectController] {type} forcibly removed from {gameObject.name}.");
    }


    /// <summary>
    /// Returns true if the specified status is currently active on this character.
    /// </summary>
    public bool HasStatus(StatusType type)
    {
        return _activeEffects.ContainsKey(type) || _stackedEffectTypes.ContainsValue(type);
    }


    /// <summary>
    /// Removes ALL active status effects immediately.
    /// Called from OnEnable() for pool safety, and can be called manually
    /// (e.g. a cleanse ability that strips all debuffs at once).
    ///
    /// Fires OnStatusRemoved for each cleared effect so listeners stay in sync.
    /// </summary>
    public void ClearAll()
    {
        // Build a list of active types first — never modify a dictionary while iterating it
        var typesToClear = new List<StatusType>(_activeEffects.Keys);

        foreach (StatusType type in typesToClear)
        {
            StopEffectCoroutine(type);
            RemoveModifierForType(type);

            // Fire the removed event so UI and AI listeners know the effect ended
            OnStatusRemoved?.Invoke(type);
        }

        _activeEffects.Clear();
        _activeCoroutines.Clear();
        _appliedModifiers.Clear();

        foreach (KeyValuePair<int, Coroutine> pair in _stackedEffectCoroutines)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);
        }

        foreach (KeyValuePair<int, Sc_Modifier> pair in _stackedEffectModifiers)
        {
            _statBlock?.RemoveModifier(pair.Value);
        }

        foreach (StatusType type in _stackedEffectTypes.Values)
        {
            OnStatusRemoved?.Invoke(type);
        }

        _stackedEffectTypes.Clear();
        _stackedEffectCoroutines.Clear();
        _stackedEffectModifiers.Clear();
    }

    #endregion                      //----------------------------------------


    #region Effect Coroutine        //----------------------------------------

    /// <summary>
    /// Runs for the lifetime of one status effect.
    /// Handles the duration countdown and DoT ticks in a single coroutine
    /// so there is always exactly one coroutine per active StatusType.
    ///
    /// Duration is read from _activeEffects[effect.Type] each tick rather than
    /// from a local variable — this is what allows Apply() to refresh the duration
    /// by writing to the dictionary without restarting the coroutine.
    /// </summary>
    private IEnumerator EffectRoutine(Sc_StatusEffect effect)
    {
        // How often we advance the duration countdown.
        // Finer resolution = smoother remaining-time display in the HUD.
        // TODO: Tune this if performance is a concern — 0.1f is safe for up to ~50 CuBots.
        const float TICK_RESOLUTION = 0.1f;

        // For DoT effects, track time since the last damage tick separately
        float timeSinceLastDamageTick = 0f;

        // Count down until duration reaches zero
        while (_activeEffects.TryGetValue(effect.Type, out float remaining) && remaining > 0f)
        {
            // Pause-aware yield — wait here until the game resumes if paused.
            // WaitUntil allocates, so we check the flag manually in a tight loop
            // only while actually paused — this path is rarely hit.
            if (_isPaused)
                yield return new WaitUntil(() => !_isPaused);

            yield return new WaitForSeconds(TICK_RESOLUTION);

            // Subtract elapsed time from the remaining duration in the dictionary.
            // This is what Apply() reads when refreshing duration.
            if (_activeEffects.ContainsKey(effect.Type))
                _activeEffects[effect.Type] -= TICK_RESOLUTION;

            // DoT tick — only for effects that deal damage over time
            if (effect.TickInterval > 0f && effect.TickDamage > 0f)
            {
                timeSinceLastDamageTick += TICK_RESOLUTION;

                if (timeSinceLastDamageTick >= effect.TickInterval)
                {
                    timeSinceLastDamageTick = 0f;
                    ApplyDoTTick(effect.TickDamage);

                    // Stop immediately if the character died from this tick —
                    // no point continuing the coroutine on a corpse
                    if (_health != null && _health.IsDead)
                        yield break;
                }
            }
        }

        // Duration expired naturally — clean up exactly like a forced Remove()
        // but without firing the coroutine stop (we're already inside it)
        if (_activeEffects.ContainsKey(effect.Type))
        {
            RemoveModifierForType(effect.Type);
            _activeEffects.Remove(effect.Type);
            _activeCoroutines.Remove(effect.Type);

            OnStatusRemoved?.Invoke(effect.Type);

            Debug.Log($"[Mb_StatusEffectController] {effect.Type} expired on {gameObject.name}.");
        }
    }


    /// <summary>
    /// Runs one independent instance of a stackable effect.
    /// Used for DoTs that should allow multiple active copies on the same target.
    /// </summary>
    private IEnumerator StackableEffectRoutine(int instanceId, Sc_StatusEffect effect)
    {
        const float TICK_RESOLUTION = 0.1f;

        float remaining = effect.Duration;
        float timeSinceLastDamageTick = 0f;

        while (remaining > 0f)
        {
            if (_isPaused)
                yield return new WaitUntil(() => !_isPaused);

            yield return new WaitForSeconds(TICK_RESOLUTION);

            remaining -= TICK_RESOLUTION;

            if (effect.TickInterval > 0f && effect.TickDamage > 0f)
            {
                timeSinceLastDamageTick += TICK_RESOLUTION;

                if (timeSinceLastDamageTick >= effect.TickInterval)
                {
                    timeSinceLastDamageTick = 0f;
                    ApplyDoTTick(effect.TickDamage);

                    if (_health != null && _health.IsDead)
                        break;
                }
            }
        }

        RemoveStackableEffectInstance(instanceId, effect.Type);
    }


    /// <summary>
    /// Deals one DoT tick of damage to this character.
    /// Guards against dead characters — the caller also checks IsDead, but this
    /// is a second safety net in case health changes between the check and the call.
    /// </summary>
    private void ApplyDoTTick(float damage)
    {
        if (_health == null || _health.IsDead) return;

        _health.TakeDamage(damage);
    }

    #endregion                      //----------------------------------------


    #region Internal Helpers        //----------------------------------------

    /// <summary>
    /// Starts a new independent instance of a stackable effect.
    /// </summary>
    private void ApplyStackableEffect(Sc_StatusEffect effect)
    {
        int instanceId = _nextStackedEffectId++;

        _stackedEffectTypes[instanceId] = effect.Type;

        if (effect.StatModifier != null && _statBlock != null)
        {
            _statBlock.AddModifier(effect.StatModifier);
            _stackedEffectModifiers[instanceId] = effect.StatModifier;
        }

        Coroutine routine = StartCoroutine(StackableEffectRoutine(instanceId, effect));
        _stackedEffectCoroutines[instanceId] = routine;

        OnStatusApplied?.Invoke(effect.Type);

        Debug.Log($"[Mb_StatusEffectController] {effect.Type} stack applied to {gameObject.name} " +
                  $"({effect.Duration}s).");
    }


    /// <summary>
    /// Removes one stackable effect instance after expiry, death, or forced removal.
    /// </summary>
    private void RemoveStackableEffectInstance(int instanceId, StatusType type)
    {
        if (_stackedEffectModifiers.TryGetValue(instanceId, out Sc_Modifier modifier))
        {
            _statBlock?.RemoveModifier(modifier);
            _stackedEffectModifiers.Remove(instanceId);
        }

        _stackedEffectTypes.Remove(instanceId);
        _stackedEffectCoroutines.Remove(instanceId);

        OnStatusRemoved?.Invoke(type);

        Debug.Log($"[Mb_StatusEffectController] {type} stack expired on {gameObject.name}.");
    }


    /// <summary>
    /// Removes all stackable instances matching the given status type.
    /// </summary>
    private bool RemoveStackableEffectsOfType(StatusType type)
    {
        List<int> idsToRemove = new List<int>();

        foreach (KeyValuePair<int, StatusType> pair in _stackedEffectTypes)
        {
            if (pair.Value == type)
                idsToRemove.Add(pair.Key);
        }

        foreach (int instanceId in idsToRemove)
        {
            if (_stackedEffectCoroutines.TryGetValue(instanceId, out Coroutine routine) && routine != null)
                StopCoroutine(routine);

            RemoveStackableEffectInstance(instanceId, type);
        }

        return idsToRemove.Count > 0;
    }


    /// <summary>
    /// Stops the running coroutine for the given status type, if one exists.
    /// Safe to call even if no coroutine is tracked for that type.
    /// </summary>
    private void StopEffectCoroutine(StatusType type)
    {
        if (_activeCoroutines.TryGetValue(type, out Coroutine routine) && routine != null)
        {
            StopCoroutine(routine);
            _activeCoroutines.Remove(type);
        }
    }


    /// <summary>
    /// Removes the stat modifier we applied for the given status type, if one exists.
    /// Safe to call even if no modifier was applied (e.g. pure DoT effects like Poison).
    /// </summary>
    private void RemoveModifierForType(StatusType type)
    {
        if (_appliedModifiers.TryGetValue(type, out Sc_Modifier modifier))
        {
            // Guard: StatBlock's RemoveModifier() is a no-op if the modifier
            // isn't in its list, but we only call it when we know we put it there
            _statBlock?.RemoveModifier(modifier);
            _appliedModifiers.Remove(type);
        }
    }


    /// <summary>
    /// Replaces the stat modifier for an already-active status effect.
    /// This keeps reapplications from stacking while still allowing a refreshed
    /// status to use the newest slow strength.
    /// </summary>
    private void ReplaceModifierForType(StatusType type, Sc_Modifier modifier)
    {
        RemoveModifierForType(type);

        if (modifier == null || _statBlock == null) return;

        _statBlock.AddModifier(modifier);
        _appliedModifiers[type] = modifier;
    }

    #endregion                      //----------------------------------------


    #region Pause Handling          //----------------------------------------

    private void HandlePause() => _isPaused = true;
    private void HandleResume() => _isPaused = false;

    #endregion                      //----------------------------------------
}
