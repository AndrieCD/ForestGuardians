using System.Collections;
using UnityEngine;

public class Sc_BaseAbility
{
    [Header("Base Ability Object")]
    [SerializeField] protected SO_Ability _AbilityData;
    protected Mb_CharacterBase _User;
    protected float _Cooldown;
    protected float _CooldownRemaining = 0;

    public Sc_BaseAbility(SO_Ability abilityObject, Mb_CharacterBase user)
    {
        _AbilityData = abilityObject;
        _User = user;
    }

    // 1. Called when the Character spawns (Setup Passive)
    public virtual void OnEquip(Mb_CharacterBase user) { }

    // 2. Called when the Player presses the button (Active)
    public virtual void Activate(Mb_CharacterBase user) { }

    // 3. Called when Character dies or ability is swapped (Cleanup)
    public virtual void OnUnequip(Mb_CharacterBase user) { }

    protected bool CheckCooldown( )
    {
        if (_CooldownRemaining > 0)
        {
            //Debug.Log($"{this._AbilityData.AbilityName} is on cooldown for {_CooldownRemaining} more seconds.");
            return false;
        }
        return true;
    }

    protected IEnumerator RefreshCooldown()
    {
        while (_CooldownRemaining > 0)
        {
            yield return new WaitForSeconds(0.1f);
            _CooldownRemaining -= 0.1f;
        }
        _CooldownRemaining = 0;
    }

    protected void StartCooldown(MonoBehaviour user)
    {
        // Start cooldown
        _CooldownRemaining = _Cooldown;
        user.StartCoroutine(RefreshCooldown( ));
    }
}
