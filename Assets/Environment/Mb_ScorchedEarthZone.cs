// Mb_ScorchedEarthZone.cs
// Applies a burn DoT effect to any character (Guardian or CuBot) that enters
// a scorched ground patch. The burn is removed when they leave the zone.
//
// HOW IT WORKS:
//   - OnTriggerEnter: find the character's Mb_StatusEffectController and call
//     Apply() with a Burn status effect. The controller owns the burn timer —
//     we just tell it to start.
//   - OnTriggerExit: call Remove(StatusType.Burn) on the controller. The burn
//     stops immediately rather than running out its duration.
//   - OnDeath: if a character dies inside the zone, force-remove the burn.
//     This is the CuBot pool safety path — a pooled CuBot's StatusEffectController
//     calls ClearAll() on OnEnable() anyway, but we remove it here proactively
//     so the effect doesn't tick on a corpse between death and pool return.
//
// WHY WE DON'T USE AN INFINITE BURN DURATION:
//   Sc_StatusEffect.Burn() takes a duration parameter. We pass BurnDuration
//   (default 3f) rather than infinity so that if Remove() is somehow missed
//   (e.g. a frame-perfect exit during a lag spike), the burn naturally expires
//   shortly after. The StatusEffectController's reapplication rule refreshes
//   the duration on each re-enter, so the burn never expires while the character
//   is still inside.
//
// WHY WE DON'T TRACK THE MODIFIER DIRECTLY:
//   Unlike Mb_RiverZone, we don't hold a Sc_Modifier reference here.
//   Mb_StatusEffectController owns the modifier for status effects — we interact
//   with it only through Apply() and Remove(). This keeps responsibilities clean.
//
// Inspector setup:
//   - Attach this script to any scorched ground patch GameObject.
//   - Add a Collider with "Is Trigger" checked. A Box Collider sized to the
//     visible burn patch works well. Keep it shallow (low Y scale) so it only
//     catches characters standing on the ground, not jumping over it.
//   - BurnTickDamage: damage dealt per tick. // TODO: Tune — start low (e.g. 5f)
//     and increase based on playtesting. Should feel threatening but not instant death.
//   - BurnTickInterval: seconds between damage ticks. // TODO: Tune — 0.5f (default)
//     means 2 ticks per second. Increase for a slower, heavier burn feel.
//   - BurnDuration: how long the burn persists after the character exits the zone.
//     Default 3f. Keep this short — it is a safety net, not a lingering punishment.

using System.Collections.Generic;
using UnityEngine;

public class Mb_ScorchedEarthZone : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Burn Settings")]
    [Tooltip("Damage dealt to the character per burn tick.")]
    [SerializeField]
    // TODO: Tune — 5f is a gentle starting value. Raise for higher difficulty stages.
    private float BurnTickDamage = 5f;

    [Tooltip("Seconds between each burn damage tick.")]
    [SerializeField]
    // TODO: Tune — 0.5f = 2 ticks per second. Higher = slower, punchier burn.
    private float BurnTickInterval = 0.5f;

    [Tooltip("How long the burn effect persists after the character leaves the zone. " +
             "Acts as a safety net if Remove() is missed. Keep short.")]
    [SerializeField]
    // TODO: Tune alongside BurnTickInterval. 3f gives ~6 ticks after exit at 0.5s interval.
    private float BurnDuration = 3f;


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    // Tracks every character currently inside this zone.
    // We use a HashSet because we only need membership checks and fast removal —
    // we don't need to store a modifier reference (the controller owns that).
    private readonly HashSet<Mb_CharacterBase> _charactersInZone
        = new HashSet<Mb_CharacterBase>();


    // -------------------------------------------------------------------------
    // Trigger Detection
    // -------------------------------------------------------------------------

    private void OnTriggerStay(Collider other)
    {
        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null) return;

        // Already tracked — multiple colliders on one character could fire this twice
        //if (_charactersInZone.Contains(character)) return;

        // Don't apply burn to a character that's already dead
        if (character.Health.IsDead) return;

        ApplyBurn(character);
    }


    private void OnTriggerExit(Collider other)
    {
        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null) return;

        RemoveBurn(character);
    }


    // -------------------------------------------------------------------------
    // Burn Application / Removal
    // -------------------------------------------------------------------------

    private void ApplyBurn(Mb_CharacterBase character)
    {
        // StatusEffectController is optional — CuBots or Guardians might not have
        // one yet during early development. Warn and skip rather than throwing.
        Mb_StatusEffectController statusController =
            character.GetComponent<Mb_StatusEffectController>();

        if (statusController == null)
        {
            Debug.LogWarning($"[Mb_ScorchedEarthZone] {character.name} has no " +
                             $"Mb_StatusEffectController — burn skipped.");
            return;
        }

        _charactersInZone.Add(character);

        // Build a fresh Burn effect using the static factory on Sc_StatusEffect.
        // BurnDuration here is the safety-net duration — the burn is removed explicitly
        // on exit via RemoveBurn(), so this duration only matters if exit is missed.
        Sc_StatusEffect burnEffect = Sc_StatusEffect.Burn(
            BurnDuration,
            BurnTickDamage,
            BurnTickInterval
        );

        statusController.Apply(burnEffect);

        // Subscribe to death so we can clean up if the character dies inside the zone.
        // CuBot pool safety: Mb_StatusEffectController.ClearAll() also runs on OnEnable,
        // but we remove proactively here so burn doesn't tick on a fresh corpse.
        character.Health.OnDeath += () => HandleCharacterDeath(character);

        Debug.Log($"[Mb_ScorchedEarthZone] Burn applied to {character.name}.");
    }


    private void RemoveBurn(Mb_CharacterBase character)
    {
        // Character may not be in our set if ApplyBurn was skipped
        // (e.g. missing StatusEffectController) — exit silently.
        if (!_charactersInZone.Contains(character)) return;

        _charactersInZone.Remove(character);

        Mb_StatusEffectController statusController =
            character.GetComponent<Mb_StatusEffectController>();

        // Guard: controller may have been destroyed between enter and exit
        // (unlikely but possible during scene teardown).
        if (statusController == null) return;

        statusController.Remove(StatusType.Burn);

        Debug.Log($"[Mb_ScorchedEarthZone] Burn removed from {character.name}.");
    }


    private void HandleCharacterDeath(Mb_CharacterBase character)
    {
        // Force-remove burn when a character dies inside the zone.
        // RemoveBurn() handles the null and membership checks for us.
        RemoveBurn(character);
    }


    // -------------------------------------------------------------------------
    // Scene Cleanup
    // -------------------------------------------------------------------------

    private void OnDisable()
    {
        // Zone disabled mid-stage (e.g. stage teardown) — remove burn from
        // every character still inside so no one carries a burn modifier
        // into the next scene or wave.
        var characters = new List<Mb_CharacterBase>(_charactersInZone);
        foreach (var character in characters)
            RemoveBurn(character);
    }
}