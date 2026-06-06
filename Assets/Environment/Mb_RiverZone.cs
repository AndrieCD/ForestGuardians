// Mb_RiverZone.cs
// Applies a movement speed slow to any character (Guardian or CuBot)
// that enters the river trigger zone. The slow is removed when they leave.
//
// HOW IT WORKS:
//   - OnTriggerEnter: builds a fresh Sc_Modifier per character and stores it
//     in a dictionary keyed by the character reference. Applies it via Mb_StatBlock.
//   - OnTriggerExit: retrieves and removes that exact modifier for that character.
//   - OnDeath subscription: if a character dies while still inside, the modifier
//     is force-removed before the GameObject deactivates. This is critical for
//     CuBot pool safety — a pooled CuBot must never carry leftover modifiers
//     into its next wave.
//
// Inspector setup:
//   - Attach this script to the river plane GameObject (or a child trigger volume).
//   - The river plane needs a Collider with "Is Trigger" checked.
//   - SlowPercent: fraction of MoveSpeed to remove (0.25 = 25% slower). Tune in Inspector.
//
// NOTE: The river plane's visual mesh should NOT have a Collider with Is Trigger
// if it also needs to block physics (e.g. prevent falling through). Use a separate
// child GameObject for the trigger volume if collision and trigger are both needed.

using System.Collections.Generic;
using UnityEngine;

public class Mb_RiverZone : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [SerializeField]
    [Tooltip("Fraction of MoveSpeed to remove while inside the river. 0.25 = 25% slower.")]
    // TODO: Tune this value. 0.25 is a starting point — playtest for feel.
    private float SlowPercent = 0.25f;


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    // Maps each character currently inside the zone to the exact modifier we applied.
    // We need the exact reference to remove it later — we can't just remove "any slow."
    private readonly Dictionary<Mb_CharacterBase, Sc_Modifier> _activeSlows
        = new Dictionary<Mb_CharacterBase, Sc_Modifier>();


    // -------------------------------------------------------------------------
    // Trigger Detection
    // -------------------------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        // Only affect characters — skip terrain, projectiles, etc.
        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null) return;

        // Already tracking this character (e.g. multiple colliders on one character) — skip
        if (_activeSlows.ContainsKey(character)) return;

        // Dead characters don't need slowing
        if (character.Health.IsDead) return;

        ApplySlow(character);

        // if character is burning, remove burn when touching this river
        Mb_StatusEffectController statusController =
            character.GetComponent<Mb_StatusEffectController>();
        if (statusController != null && statusController.HasStatus(StatusType.Burn))
        {
            RemoveBurn(character);
        }
    }


    private void OnTriggerExit(Collider other)
    {
        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null) return;

        RemoveSlow(character);
    }


    // -------------------------------------------------------------------------
    // Slow Application / Removal
    // -------------------------------------------------------------------------

    private void ApplySlow(Mb_CharacterBase character)
    {
        // Build a fresh modifier instance for this character.
        // Each character in the zone gets their own instance so removing one
        // doesn't accidentally remove another character's slow.
        Sc_Modifier slowModifier = new Sc_Modifier(
            "River Slow",
            ModifierSource.Environmental,
            new System.Collections.Generic.List<Sc_StatEffect>
            {
                // Negative Percent reduces the stat multiplicatively.
                // e.g. -0.25 on a 10 MS stat = 10 * (1 - 0.25) = 7.5 effective MS.
                new Sc_StatEffect(StatType.MoveSpeed, -SlowPercent, StatModType.Percent)
            }
        );

        _activeSlows[character] = slowModifier;
        character.Stats.AddModifier(slowModifier);

        // Subscribe to death so we can clean up if this character dies inside the zone.
        // Pool safety: a CuBot returned to the pool while slowed would carry the
        // modifier into its next wave if we don't remove it here.
        character.Health.OnDeath += () => HandleCharacterDeath(character);

        Debug.Log($"[Mb_RiverZone] Applied river slow to {character.name}.");
    }


    private void RemoveSlow(Mb_CharacterBase character)
    {
        if (!_activeSlows.TryGetValue(character, out Sc_Modifier modifier)) return;

        character.Stats.RemoveModifier(modifier);
        _activeSlows.Remove(character);

        // We can't unsubscribe the anonymous lambda we used in ApplySlow,
        // but since we remove the dictionary entry here, HandleCharacterDeath
        // will find no entry and exit safely if it somehow fires afterward.

        Debug.Log($"[Mb_RiverZone] Removed river slow from {character.name}.");
    }


    private void HandleCharacterDeath(Mb_CharacterBase character)
    {
        // Character died while inside the zone — force-remove the slow.
        // This is the pool safety path for CuBots: the modifier must be gone
        // before the GameObject is deactivated and returned to the pool.
        RemoveSlow(character);
    }


    private void RemoveBurn(Mb_CharacterBase character)
    {
        // Character may not be in our set if ApplyBurn was skipped
        // (e.g. missing StatusEffectController) — exit silently.
        if (!_activeSlows.ContainsKey(character)) return;

        _activeSlows.Remove(character);

        Mb_StatusEffectController statusController =
            character.GetComponent<Mb_StatusEffectController>();

        // Guard: controller may have been destroyed between enter and exit
        // (unlikely but possible during scene teardown).
        if (statusController == null) return;

        statusController.Remove(StatusType.Burn);

        Debug.Log($"[Mb_ScorchedEarthZone] Burn removed from {character.name}.");
    }

    // -------------------------------------------------------------------------
    // Scene Cleanup
    // -------------------------------------------------------------------------

    private void OnDisable()
    {
        // If the zone is disabled mid-wave (e.g. stage teardown), remove all
        // active slows so no character is left with a permanent slow modifier.
        var characters = new List<Mb_CharacterBase>(_activeSlows.Keys);
        foreach (var character in characters)
            RemoveSlow(character);
    }
}