using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 2 WindGust event for the active Guardian and spawned CuBots.
/// Attach this to a GameObject in the Stage 2 scene.
/// </summary>
public class Mb_Stage2WindGustEvent : MonoBehaviour
{
    [Header("Passive Wind")]
    [Tooltip("Passive MoveSpeed increase applied to Guardians and CuBots. 0.20 = 20% faster.")]
    [SerializeField] private float PassiveMoveSpeedBonus = 0.20f;

    [Header("WindGust Burst")]
    [Tooltip("MoveSpeed increase while WindGust is active. 0.50 = 50% faster.")]
    [SerializeField] private float GustMoveSpeedBonus = 0.50f;

    [Tooltip("AttackSpeed increase while WindGust is active. 0.20 = 20% faster.")]
    [SerializeField] private float GustAttackSpeedBonus = 0.20f;

    [Tooltip("Minimum seconds before the next WindGust starts.")]
    [SerializeField] private float MinGustInterval = 20f;

    [Tooltip("Maximum seconds before the next WindGust starts.")]
    [SerializeField] private float MaxGustInterval = 30f;

    [Tooltip("Seconds that each WindGust burst lasts.")]
    [SerializeField] private float GustDuration = 3f;

    private const string PASSIVE_MODIFIER_NAME = "WindGust Passive Wind";
    private const string GUST_MODIFIER_NAME = "WindGust Burst";

    private readonly Dictionary<Mb_CharacterBase, Sc_Modifier> _activeModifiers =
        new Dictionary<Mb_CharacterBase, Sc_Modifier>();

    private readonly Dictionary<Mb_CharacterBase, Action> _deathHandlers =
        new Dictionary<Mb_CharacterBase, Action>();

    private Coroutine _windGustRoutine;
    private bool _isGustActive;

    private void OnEnable()
    {
        Mb_GuardianBase.OnActiveGuardianChanged += HandleActiveGuardianChanged;
        MB_CuBotBase.OnCuBotSpawn += HandleCuBotSpawn;
        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath;

        ApplyCurrentWindModifier(Mb_GuardianBase.CurrentGuardian);
        _windGustRoutine = StartCoroutine(WindGustRoutine());
    }

    private void OnDisable()
    {
        Mb_GuardianBase.OnActiveGuardianChanged -= HandleActiveGuardianChanged;
        MB_CuBotBase.OnCuBotSpawn -= HandleCuBotSpawn;
        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;

        if (_windGustRoutine != null)
        {
            StopCoroutine(_windGustRoutine);
            _windGustRoutine = null;
        }

        _isGustActive = false;
        RemoveAllWindModifiers();
    }

    private void HandleActiveGuardianChanged(Mb_GuardianBase guardian)
    {
        RemoveInactiveGuardians(guardian);
        ApplyCurrentWindModifier(guardian);
    }

    private void HandleCuBotSpawn(GameObject cuBotObject)
    {
        if (!isActiveAndEnabled) return;
        if (cuBotObject == null) return;

        StartCoroutine(ApplyCuBotWindAfterReset(cuBotObject));
    }

    private IEnumerator ApplyCuBotWindAfterReset(GameObject cuBotObject)
    {
        yield return null;

        if (!isActiveAndEnabled) yield break;
        if (cuBotObject == null || !cuBotObject.activeInHierarchy) yield break;

        MB_CuBotBase cuBot = cuBotObject.GetComponent<MB_CuBotBase>();
        ApplyCurrentWindModifier(cuBot);
    }

    private void HandleCuBotDeath(GameObject cuBotObject)
    {
        if (cuBotObject == null) return;

        MB_CuBotBase cuBot = cuBotObject.GetComponent<MB_CuBotBase>();
        RemoveWindModifier(cuBot);
    }

    private IEnumerator WindGustRoutine()
    {
        while (true)
        {
            float interval = UnityEngine.Random.Range(MinGustInterval, MaxGustInterval);
            yield return new WaitForSeconds(interval);

            ActivateGust();
            yield return new WaitForSeconds(GustDuration);
            DeactivateGust();
        }
    }

    private void ActivateGust()
    {
        _isGustActive = true;
        RefreshAllWindModifiers();
    }

    private void DeactivateGust()
    {
        _isGustActive = false;
        RefreshAllWindModifiers();
    }

    private void ApplyCurrentWindModifier(Mb_CharacterBase character)
    {
        if (character == null) return;
        if (character.Stats == null) return;
        if (character.Health != null && character.Health.IsDead) return;

        RemoveWindModifier(character);

        Sc_Modifier modifier = _isGustActive
            ? CreateGustModifier()
            : CreatePassiveModifier();

        _activeModifiers[character] = modifier;
        character.Stats.AddModifier(modifier);
        SubscribeToDeath(character);
    }

    private Sc_Modifier CreatePassiveModifier()
    {
        return new Sc_Modifier(
            PASSIVE_MODIFIER_NAME,
            ModifierSource.StageScaling,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.MoveSpeed, PassiveMoveSpeedBonus, StatModType.Percent)
            }
        );
    }

    private Sc_Modifier CreateGustModifier()
    {
        return new Sc_Modifier(
            GUST_MODIFIER_NAME,
            ModifierSource.StageScaling,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.MoveSpeed, GustMoveSpeedBonus, StatModType.Percent),
                new Sc_StatEffect(StatType.AttackSpeed, GustAttackSpeedBonus, StatModType.Percent)
            }
        );
    }

    private void RemoveWindModifier(Mb_CharacterBase character)
    {
        if (character == null) return;
        if (!_activeModifiers.TryGetValue(character, out Sc_Modifier modifier)) return;

        if (character.Stats != null)
            character.Stats.RemoveModifier(modifier);

        _activeModifiers.Remove(character);
        UnsubscribeFromDeath(character);
    }

    private void RefreshAllWindModifiers()
    {
        List<Mb_CharacterBase> characters = new List<Mb_CharacterBase>(_activeModifiers.Keys);

        foreach (Mb_CharacterBase character in characters)
            ApplyCurrentWindModifier(character);
    }

    private void RemoveInactiveGuardians(Mb_GuardianBase activeGuardian)
    {
        List<Mb_CharacterBase> characters = new List<Mb_CharacterBase>(_activeModifiers.Keys);

        foreach (Mb_CharacterBase character in characters)
        {
            if (character is Mb_GuardianBase guardian && guardian != activeGuardian)
                RemoveWindModifier(guardian);
        }
    }

    private void SubscribeToDeath(Mb_CharacterBase character)
    {
        if (character == null) return;
        if (character.Health == null) return;
        if (_deathHandlers.ContainsKey(character)) return;

        Action handler = () => RemoveWindModifier(character);
        _deathHandlers[character] = handler;
        character.Health.OnDeath += handler;
    }

    private void UnsubscribeFromDeath(Mb_CharacterBase character)
    {
        if (character == null) return;
        if (character.Health == null) return;
        if (!_deathHandlers.TryGetValue(character, out Action handler)) return;

        character.Health.OnDeath -= handler;
        _deathHandlers.Remove(character);
    }

    private void RemoveAllWindModifiers()
    {
        List<Mb_CharacterBase> characters = new List<Mb_CharacterBase>(_activeModifiers.Keys);

        foreach (Mb_CharacterBase character in characters)
            RemoveWindModifier(character);

        _deathHandlers.Clear();
    }
}
