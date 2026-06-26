// Mb_RiverZone.cs
// Applies a movement slow to characters inside a river trigger.
//
// Responsibilities:
//   - Apply one Environmental MoveSpeed modifier per character while inside.
//   - Remove that exact modifier on exit, death, or zone disable.
//   - Clear Burn when a burning character touches the river.
//
// Inspector setup:
//   - Attach this script to the river trigger volume.
//   - The collider must have "Is Trigger" enabled.
//   - SlowPercent is the fraction of MoveSpeed removed while inside.

using System;
using System.Collections.Generic;
using UnityEngine;

public class Mb_RiverZone : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Fraction of MoveSpeed to remove while inside the river. 0.25 = 25% slower.")]
    private float SlowPercent = 0.25f;

    private readonly Dictionary<Mb_CharacterBase, Sc_Modifier> _activeSlows
        = new Dictionary<Mb_CharacterBase, Sc_Modifier>();

    private readonly Dictionary<Mb_CharacterBase, Action> _deathHandlers
        = new Dictionary<Mb_CharacterBase, Action>();

    private void OnTriggerEnter(Collider other)
    {
        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null) return;
        if (_activeSlows.ContainsKey(character)) return;
        if (character.Health.IsDead) return;

        RemoveBurn(character);
        ApplySlow(character);
    }

    private void OnTriggerExit(Collider other)
    {
        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null) return;

        RemoveSlow(character);
    }

    private void ApplySlow(Mb_CharacterBase character)
    {
        Sc_Modifier slowModifier = new Sc_Modifier(
            "River Slow",
            ModifierSource.Environmental,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.MoveSpeed, -SlowPercent, StatModType.Percent)
            }
        );

        _activeSlows[character] = slowModifier;
        character.Stats.AddModifier(slowModifier);
        SubscribeToDeath(character);
    }

    private void RemoveSlow(Mb_CharacterBase character)
    {
        if (!_activeSlows.TryGetValue(character, out Sc_Modifier modifier)) return;

        character.Stats.RemoveModifier(modifier);
        _activeSlows.Remove(character);
        UnsubscribeFromDeath(character);
    }

    private void RemoveBurn(Mb_CharacterBase character)
    {
        Mb_StatusEffectController statusController =
            character.GetComponent<Mb_StatusEffectController>();

        if (statusController == null) return;

        statusController.Remove(StatusType.Burn);
    }

    private void SubscribeToDeath(Mb_CharacterBase character)
    {
        if (_deathHandlers.ContainsKey(character)) return;

        Action handler = () => HandleCharacterDeath(character);
        _deathHandlers[character] = handler;
        character.Health.OnDeath += handler;
    }

    private void UnsubscribeFromDeath(Mb_CharacterBase character)
    {
        if (!_deathHandlers.TryGetValue(character, out Action handler)) return;

        character.Health.OnDeath -= handler;
        _deathHandlers.Remove(character);
    }

    private void HandleCharacterDeath(Mb_CharacterBase character)
    {
        RemoveSlow(character);
    }

    private void OnDisable()
    {
        var characters = new List<Mb_CharacterBase>(_activeSlows.Keys);
        foreach (Mb_CharacterBase character in characters)
            RemoveSlow(character);

        _deathHandlers.Clear();
    }
}
