// Mb_ScorchedEarthZone.cs
// Applies Burn to characters standing inside a scorched ground trigger.
//
// Responsibilities:
//   - Apply Burn when a character enters the zone.
//   - Refresh Burn only near expiry while the character remains inside.
//   - Remove Burn on exit, death, or zone disable.
//
// Inspector setup:
//   - Attach this script to a scorched ground trigger volume.
//   - The collider must have "Is Trigger" enabled.
//   - Tune BurnTickDamage, BurnTickInterval, and BurnDuration per stage.

using System;
using System.Collections.Generic;
using UnityEngine;

public class Mb_ScorchedEarthZone : MonoBehaviour
{
    [Header("Burn Settings")]
    [Tooltip("Damage dealt to the character per burn tick.")]
    [SerializeField] private float BurnTickDamage = 5f;

    [Tooltip("Seconds between each burn damage tick.")]
    [SerializeField] private float BurnTickInterval = 0.5f;

    [Tooltip("How long the burn effect persists after the character leaves the zone. Keep short.")]
    [SerializeField] private float BurnDuration = 3f;

    private readonly HashSet<Mb_CharacterBase> _charactersInZone
        = new HashSet<Mb_CharacterBase>();

    private readonly Dictionary<Mb_CharacterBase, Action> _deathHandlers
        = new Dictionary<Mb_CharacterBase, Action>();

    private void OnTriggerEnter(Collider other)
    {
        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null) return;

        TryStartBurn(character);
    }

    private void OnTriggerStay(Collider other)
    {
        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null) return;

        TryStartBurn(character);
        RefreshBurnIfNeeded(character);
    }

    private void OnTriggerExit(Collider other)
    {
        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null) return;

        RemoveBurn(character);
    }

    private void TryStartBurn(Mb_CharacterBase character)
    {
        if (character.Health.IsDead) return;
        if (_charactersInZone.Contains(character)) return;

        Mb_StatusEffectController statusController = GetStatusController(character);
        if (statusController == null) return;

        _charactersInZone.Add(character);
        SubscribeToDeath(character);
        ApplyBurn(statusController);
    }

    private void RefreshBurnIfNeeded(Mb_CharacterBase character)
    {
        if (!_charactersInZone.Contains(character)) return;
        if (character.Health.IsDead) return;

        Mb_StatusEffectController statusController = GetStatusController(character);
        if (statusController == null) return;

        if (!statusController.ActiveStatuses.TryGetValue(StatusType.Burn, out float remainingDuration))
        {
            ApplyBurn(statusController);
            return;
        }

        float refreshThreshold = Mathf.Max(BurnTickInterval, 0.1f);
        if (remainingDuration <= refreshThreshold)
            ApplyBurn(statusController);
    }

    private void ApplyBurn(Mb_StatusEffectController statusController)
    {
        Sc_StatusEffect burnEffect = Sc_StatusEffect.Burn(
            BurnDuration,
            BurnTickDamage,
            BurnTickInterval
        );

        statusController.Apply(burnEffect);
    }

    private void RemoveBurn(Mb_CharacterBase character)
    {
        if (!_charactersInZone.Remove(character)) return;

        Mb_StatusEffectController statusController = GetStatusController(character);
        if (statusController != null)
            statusController.Remove(StatusType.Burn);

        UnsubscribeFromDeath(character);
    }

    private Mb_StatusEffectController GetStatusController(Mb_CharacterBase character)
    {
        Mb_StatusEffectController statusController =
            character.GetComponent<Mb_StatusEffectController>();

        if (statusController == null)
        {
            Debug.LogWarning($"[Mb_ScorchedEarthZone] {character.name} has no Mb_StatusEffectController. Burn skipped.");
        }

        return statusController;
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
        RemoveBurn(character);
    }

    private void OnDisable()
    {
        var characters = new List<Mb_CharacterBase>(_charactersInZone);
        foreach (Mb_CharacterBase character in characters)
            RemoveBurn(character);

        _deathHandlers.Clear();
    }
}
