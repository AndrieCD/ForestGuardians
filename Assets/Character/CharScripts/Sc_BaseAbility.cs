using System.Collections;
using UnityEngine;

public class Sc_BaseAbility
{
    [Header("Base Ability Object")]
    [SerializeField] protected SO_Ability _AbilityData;
    protected Mb_CharacterBase _User;
    protected float _Cooldown;
    protected float _CooldownRemaining = 0;
    protected int _currentAbilityLevel;

    public Sc_BaseAbility(SO_Ability abilityObject, Mb_CharacterBase user)
    {
        _AbilityData = abilityObject;
        _User = user;
        _currentAbilityLevel = 1;
    }

    /// <summary>
    /// Called when the ability is equipped by the Player (Initialization) for passive effects.
    /// </summary>
    /// <param name="user"></param>
    public virtual void OnEquip(Mb_CharacterBase user) { }

    /// <summary>
    /// Called when the Player activates the ability (Active). Check cooldown here and apply effects.
    /// </summary>
    /// <param name="user"></param>
    public virtual void Activate(Mb_CharacterBase user) { }

    /// <summary>
    /// Called when the ability is unequipped by the Player (Cleanup) for passive effects or when the character dies.
    /// </summary>
    /// <param name="user"></param>
    public virtual void OnUnequip(Mb_CharacterBase user) { }

    /// <summary>
    /// Checks if the ability is off cooldown and can be activated. Should be called at the start of Activate().
    /// </summary>
    /// <returns></returns>
    protected bool CheckCooldown( )
    {
        if (_CooldownRemaining > 0)
        {
            //Debug.Log($"{this._AbilityData.AbilityName} is on cooldown for {_CooldownRemaining} more seconds.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Coroutine to handle cooldown timing. Decreases _CooldownRemaining over time until it reaches 0.
    /// </summary>
    /// <returns></returns>
    protected IEnumerator RefreshCooldown()
    {
        while (_CooldownRemaining > 0)
        {
            yield return new WaitForSeconds(0.1f);
            _CooldownRemaining -= 0.1f;
        }
        _CooldownRemaining = 0;
    }

    /// <summary>
    /// Starts the cooldown for the ability. Should be called at the end of Activate() after applying effects.
    /// </summary>
    /// <param name="user"></param> user refers to a character with MonoBehaviour script using the ability, needed to start the coroutine for cooldown timing.
    protected void StartCooldown(MonoBehaviour user)
    {
        // Start cooldown
        _CooldownRemaining = _Cooldown;
        user.StartCoroutine(RefreshCooldown( ));
    }
}
