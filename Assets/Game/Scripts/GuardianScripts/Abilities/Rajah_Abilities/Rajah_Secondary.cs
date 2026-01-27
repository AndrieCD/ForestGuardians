using UnityEditor.VersionControl;
using UnityEngine;

public class Rajah_Secondary : Sc_BaseAbility
{
    GameObject projectilePrefab;

    // Constructor for initialization
    public Rajah_Secondary(SO_Ability abilityObject, Mb_GuardianBase user) : base(abilityObject, user)
    {
        projectilePrefab = Resources.Load<GameObject>("FeatherPrototype");
        _Cooldown = 1 / user.AttackSpeed.Value( );
    }

    // Called when the Player spawns (Setup Passive)
    public override void OnEquip(Mb_GuardianBase user)
    {
        // Debug
        Debug.Log($"{user.name} has equipped {this._AbilityData.AbilityName}.");
    }

    // Called when the Player presses the button (Active)
    public override void Activate(Mb_GuardianBase user)
    {
        // Debug
        Debug.Log($"{user.name} has activated {this._AbilityData.AbilityName}.");
        if (CheckCooldown( ))   
            CreateProjectile(user);
    }

    private void CreateProjectile(Mb_GuardianBase user)
    {
        // Instantiate projectile
        if (projectilePrefab != null)
        {
            // Instantiate the projectile at the user's position and rotation
            Transform transform = GameObject.Find("ProjectileOrigin").transform;
            GameObject projectileInstance = GameObject.Instantiate(projectilePrefab, transform.position, transform.rotation);
            Mb_Projectile mb_Projectile = projectileInstance.GetComponent<Mb_Projectile>( );
            if (mb_Projectile != null)
            {
                mb_Projectile.SetDamageAmount(user.AttackPower.Value( ) * _AbilityData.ATKScaling);
            }
        }

        // Start cooldown
        _CooldownRemaining = _Cooldown;
        user.StartCoroutine(RefreshCooldown( ));
    }

    // Called when Character dies (Cleanup)
    public override void OnUnequip(Mb_GuardianBase user)
    {
        Debug.Log($"{user.name} has unequipped {this._AbilityData.AbilityName}.");
    }
}
