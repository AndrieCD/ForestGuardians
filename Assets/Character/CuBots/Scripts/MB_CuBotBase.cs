// MB_CuBotBase.cs
using System;
using UnityEngine;

/// <summary>
/// Base class for all CuBot enemy types.
/// Reads from SO_CuBots to populate Stats and Health via the component system.
///
/// Uses object pooling — instead of being destroyed on death, CuBots are
/// deactivated and Reset() when reactivated from the pool.
///
/// Derived classes: Mb_Chopper, Mb_Hunter, Mb_Minny, etc.
/// </summary>
public class MB_CuBotBase : Mb_CharacterBase
{
    [Header("CuBot Template")]
    [SerializeField] protected SO_CuBots _CuBotTemplate;

    // Tracks whether Awake has completed so OnEnable knows if it's safe to Reset()
    private bool _isInitialized = false;

    private Mb_CharacterBase _lastAttacker = null;

    #region Events
    public static event Action<GameObject> OnCuBotSpawn;
    public static event Action<GameObject> OnCuBotDeath;
    public static event Action<Mb_CharacterBase> OnCuBotKill;
    #endregion


    protected override void Awake()
    {
        base.Awake();

        _isInitialized = true;
    }


    private void OnEnable()
    {
        OnCuBotSpawn?.Invoke(gameObject);

        if (_isInitialized)
            Reset();
    }


    protected override void InitializeFromTemplate()
    {
        if (_CuBotTemplate == null)
        {
            Debug.LogError($"[MB_CuBotBase] No SO_CuBots template assigned on {gameObject.name}.");
            return;
        }

        _CharacterName = _CuBotTemplate.CharacterName;

        Stats.RemoveAllModifiers();
        Stats.BuildFromTemplate(_CuBotTemplate);

        // Resolve player level before initializing health —
        // SetLevel() must run first so MaxHealth is at the correct scaled value
        // before Health.Initialize() snapshots it as CurrentHealth.
        int playerLevel = 1;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Mb_CharacterBase playerChar = playerObj.GetComponent<Mb_CharacterBase>();
            if (playerChar != null)
                playerLevel = playerChar.GetLevel();
            else
                Debug.LogWarning($"[MB_CuBotBase] Player found but has no Mb_CharacterBase on {gameObject.name}. Defaulting to level 1.");
        }
        else
        {
            Debug.LogWarning($"[MB_CuBotBase] No GameObject tagged 'Player' found when spawning {gameObject.name}. Defaulting to level 1.");
        }

        // Scale stats to player level BEFORE initializing health —
        // Health.Initialize() reads MaxHealth.GetValue(), so stats must be final first.
        SetLevel(playerLevel);

        // Now MaxHealth reflects the correct level, so CurrentHealth starts at the right value.
        Health.Initialize();

        _lastAttacker = null;

        Health.OnDeath -= HandleDeath;
        Health.OnDeath += HandleDeath;

        AssignAbilities();
    }


    protected virtual void AssignAbilities() { }


    protected virtual void Reset()
    {
        InitializeFromTemplate();
    }


    public void SetLastAttacker(Mb_CharacterBase attacker)
    {
        _lastAttacker = attacker;
    }


    private void HandleDeath()
    {
        OnCuBotDeath?.Invoke(gameObject);
        OnCuBotKill?.Invoke(_lastAttacker);
        gameObject.SetActive(false);
    }


    private void OnCollisionEnter(Collision collision)
    {
        Mb_Projectile projectile = collision.gameObject.GetComponent<Mb_Projectile>();
        if (projectile == null) return;

        Health.TakeDamage(projectile.GetDamageAmount());
    }


    protected void TryUsePrimaryAttack()
    {
        Abilities.ActivatePrimaryAsAI();
    }


    // Add to Mb_PlayerController.cs
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_CuBotTemplate == null) return;

        // Primary slash hitbox
        Gizmos.color = Color.red;
        Vector3 slashCenter = transform.position + transform.forward * 1.8f;
        Gizmos.DrawWireSphere(slashCenter, 1.0f);
    }
#endif
}