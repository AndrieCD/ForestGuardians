using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only Elite layer for existing CuBots.
/// Adds global Elite stat boosts, visual scale/glow, and one passive aura.
/// </summary>
public class Mb_EliteCuBot : MonoBehaviour
{
    [Serializable]
    public class EliteConfig
    {
        [Header("Stat Multipliers")]
        [Tooltip("2.0 = Elite has 200% of its normal MaxHealth.")]
        public float MaxHealthMultiplier = 2f;

        [Tooltip("1.5 = Elite has 150% of its normal AttackPower.")]
        public float AttackPowerMultiplier = 1.5f;

        [Tooltip("1.5 = Elite has 150% of its normal AbilityPower.")]
        public float AbilityPowerMultiplier = 1.5f;

        [Tooltip("1.15 = Elite has 115% of its normal MoveSpeed.")]
        public float MoveSpeedMultiplier = 1.15f;

        [Header("Visuals")]
        [Tooltip("1.25 = Elite model is scaled to 125% of normal size.")]
        public float ScaleMultiplier = 1.25f;

        [Tooltip("Color used by the generated Elite glow light and health bar.")]
        public Color EliteColor = new Color(1f, 0.1f, 0.05f, 1f);

        [Tooltip("Generated red glow radius around the Elite.")]
        public float GlowRange = 5f;

        [Tooltip("Generated red glow intensity around the Elite.")]
        public float GlowIntensity = 2f;

        [Header("Aura")]
        [Tooltip("World-space radius where nearby CuBots receive the Elite aura.")]
        public float AuraRadius = 8f;

        [Tooltip("Seconds between aura target refreshes.")]
        public float AuraRefreshInterval = 0.25f;

        [Tooltip("Commander aura MoveSpeed bonus. 0.20 = 20% faster.")]
        public float CommanderMoveSpeedBonus = 0.20f;

        [Tooltip("Commander aura AttackSpeed bonus. 0.20 = 20% faster.")]
        public float CommanderAttackSpeedBonus = 0.20f;

        [Tooltip("Guardian aura flat HealthRegen bonus.")]
        public float GuardianHealthRegenBonus = 15f;
    }

    public bool IsElite { get; private set; }
    public EliteAuraType ActiveAura { get; private set; }
    public Color EliteColor => _config != null ? _config.EliteColor : Color.red;

    private const string ELITE_MODIFIER_NAME = "Elite CuBot";
    private const string COMMANDER_AURA_MODIFIER_NAME = "Elite Commander Aura";
    private const string GUARDIAN_AURA_MODIFIER_NAME = "Elite Guardian Aura";
    private const string GLOW_OBJECT_NAME = "Elite Glow";

    private readonly Dictionary<Mb_CharacterBase, Sc_Modifier> _auraModifiers =
        new Dictionary<Mb_CharacterBase, Sc_Modifier>();

    private readonly Dictionary<Mb_CharacterBase, Action> _deathHandlers =
        new Dictionary<Mb_CharacterBase, Action>();

    private MB_CuBotBase _cuBot;
    private Mb_StatBlock _stats;
    private Mb_HealthComponent _health;
    private Mb_CuBotHealthBar _healthBar;
    private EliteConfig _config;
    private Sc_Modifier _eliteModifier;
    private Vector3 _baseScale;
    private float _auraTimer;
    private Light _glowLight;

    private void Awake()
    {
        CacheComponents();
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        ClearElite();
    }

    private void OnDisable()
    {
        ClearElite();
    }

    private void Update()
    {
        if (!IsElite || _config == null) return;

        _auraTimer -= Time.deltaTime;
        if (_auraTimer > 0f) return;

        _auraTimer = Mathf.Max(0.05f, _config.AuraRefreshInterval);
        RefreshAuraTargets();
    }

    public void ApplyElite(EliteAuraType auraType, EliteConfig config)
    {
        CacheComponents();
        ClearElite();

        if (_cuBot == null || _stats == null || _health == null || config == null) return;

        _config = config;
        ActiveAura = ResolveAura(auraType);
        IsElite = true;

        _eliteModifier = CreateEliteModifier();
        _stats.AddModifier(_eliteModifier);

        _health.Initialize();
        ApplyVisualState();
        _healthBar?.RefreshEliteState();

        _auraTimer = 0f;
        RefreshAuraTargets();
    }

    public void ClearElite()
    {
        RemoveAllAuraModifiers();

        if (_stats != null && _eliteModifier != null)
            _stats.RemoveModifier(_eliteModifier);

        _eliteModifier = null;
        IsElite = false;
        ActiveAura = EliteAuraType.Random;
        _config = null;
        _auraTimer = 0f;

        transform.localScale = _baseScale;
        SetGlowEnabled(false);
        _healthBar?.RefreshEliteState();
    }

    private void CacheComponents()
    {
        _cuBot = GetComponent<MB_CuBotBase>();
        _stats = GetComponent<Mb_StatBlock>();
        _health = GetComponent<Mb_HealthComponent>();
        _healthBar = GetComponent<Mb_CuBotHealthBar>();
    }

    private EliteAuraType ResolveAura(EliteAuraType auraType)
    {
        if (auraType != EliteAuraType.Random)
            return auraType;

        return UnityEngine.Random.value < 0.5f
            ? EliteAuraType.Commander
            : EliteAuraType.Guardian;
    }

    private Sc_Modifier CreateEliteModifier()
    {
        return new Sc_Modifier(
            ELITE_MODIFIER_NAME,
            ModifierSource.WaveScaling,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.MaxHealth, Mathf.Max(0f, _config.MaxHealthMultiplier) - 1f, StatModType.Percent),
                new Sc_StatEffect(StatType.AttackPower, Mathf.Max(0f, _config.AttackPowerMultiplier) - 1f, StatModType.Percent),
                new Sc_StatEffect(StatType.AbilityPower, Mathf.Max(0f, _config.AbilityPowerMultiplier) - 1f, StatModType.Percent),
                new Sc_StatEffect(StatType.MoveSpeed, Mathf.Max(0f, _config.MoveSpeedMultiplier) - 1f, StatModType.Percent)
            }
        );
    }

    private void ApplyVisualState()
    {
        transform.localScale = _baseScale * Mathf.Max(0.1f, _config.ScaleMultiplier);
        EnsureGlowLight();
        SetGlowEnabled(true);
    }

    private void EnsureGlowLight()
    {
        if (_glowLight != null) return;

        Transform existingGlow = transform.Find(GLOW_OBJECT_NAME);
        GameObject glowObject = existingGlow != null
            ? existingGlow.gameObject
            : new GameObject(GLOW_OBJECT_NAME);

        if (existingGlow == null)
            glowObject.transform.SetParent(transform, false);

        glowObject.transform.localPosition = Vector3.up;

        _glowLight = glowObject.GetComponent<Light>();
        if (_glowLight == null)
            _glowLight = glowObject.AddComponent<Light>();

        _glowLight.type = LightType.Point;
    }

    private void SetGlowEnabled(bool enabled)
    {
        if (_glowLight == null) return;

        _glowLight.enabled = enabled;

        if (!enabled || _config == null) return;

        _glowLight.color = _config.EliteColor;
        _glowLight.range = Mathf.Max(0f, _config.GlowRange);
        _glowLight.intensity = Mathf.Max(0f, _config.GlowIntensity);
    }

    private void RefreshAuraTargets()
    {
        if (!IsElite || _config == null) return;

        HashSet<Mb_CharacterBase> targetsInRange = new HashSet<Mb_CharacterBase>();
        Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(0f, _config.AuraRadius));

        foreach (Collider hit in hits)
        {
            MB_CuBotBase target = hit.GetComponentInParent<MB_CuBotBase>();
            if (!CanReceiveAura(target)) continue;

            targetsInRange.Add(target);

            if (!_auraModifiers.ContainsKey(target))
                ApplyAuraModifier(target);
        }

        List<Mb_CharacterBase> previousTargets = new List<Mb_CharacterBase>(_auraModifiers.Keys);
        foreach (Mb_CharacterBase target in previousTargets)
        {
            if (!targetsInRange.Contains(target))
                RemoveAuraModifier(target);
        }
    }

    private bool CanReceiveAura(MB_CuBotBase target)
    {
        if (target == null) return false;
        if (target == _cuBot) return false;
        if (!target.gameObject.activeInHierarchy) return false;
        if (target.Health == null || target.Health.IsDead) return false;
        if (target.Stats == null) return false;

        return true;
    }

    private void ApplyAuraModifier(Mb_CharacterBase target)
    {
        Sc_Modifier modifier = CreateAuraModifier();
        _auraModifiers[target] = modifier;
        target.Stats.AddModifier(modifier);
        SubscribeToDeath(target);
    }

    private Sc_Modifier CreateAuraModifier()
    {
        if (ActiveAura == EliteAuraType.Guardian)
        {
            return new Sc_Modifier(
                GUARDIAN_AURA_MODIFIER_NAME,
                ModifierSource.WaveScaling,
                new List<Sc_StatEffect>
                {
                    new Sc_StatEffect(StatType.HealthRegen, Mathf.Max(0f, _config.GuardianHealthRegenBonus), StatModType.Flat)
                }
            );
        }

        return new Sc_Modifier(
            COMMANDER_AURA_MODIFIER_NAME,
            ModifierSource.WaveScaling,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.MoveSpeed, Mathf.Max(0f, _config.CommanderMoveSpeedBonus), StatModType.Percent),
                new Sc_StatEffect(StatType.AttackSpeed, Mathf.Max(0f, _config.CommanderAttackSpeedBonus), StatModType.Percent)
            }
        );
    }

    private void RemoveAuraModifier(Mb_CharacterBase target)
    {
        if (target == null) return;
        if (!_auraModifiers.TryGetValue(target, out Sc_Modifier modifier)) return;

        if (target.Stats != null)
            target.Stats.RemoveModifier(modifier);

        _auraModifiers.Remove(target);
        UnsubscribeFromDeath(target);
    }

    private void SubscribeToDeath(Mb_CharacterBase target)
    {
        if (target == null) return;
        if (target.Health == null) return;
        if (_deathHandlers.ContainsKey(target)) return;

        Action handler = () => RemoveAuraModifier(target);
        _deathHandlers[target] = handler;
        target.Health.OnDeath += handler;
    }

    private void UnsubscribeFromDeath(Mb_CharacterBase target)
    {
        if (target == null) return;
        if (target.Health == null) return;
        if (!_deathHandlers.TryGetValue(target, out Action handler)) return;

        target.Health.OnDeath -= handler;
        _deathHandlers.Remove(target);
    }

    private void RemoveAllAuraModifiers()
    {
        List<Mb_CharacterBase> targets = new List<Mb_CharacterBase>(_auraModifiers.Keys);

        foreach (Mb_CharacterBase target in targets)
            RemoveAuraModifier(target);

        _deathHandlers.Clear();
    }
}
