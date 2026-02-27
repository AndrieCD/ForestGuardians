using UnityEngine;

public class Rajah_Secondary : Sc_BaseAbility
{
    GameObject projectilePrefab;

    // Constructor for initialization
    public Rajah_Secondary(SO_Ability abilityObject, Mb_CharacterBase user) : base(abilityObject, user)
    {
        //projectilePrefab = Resources.Load<GameObject>("FeatherPrototype");
        projectilePrefab = _AbilityData.ProjectileModel;
        _Cooldown = 1 / user.AttackSpeed.Value( );
    }

    // Called when the Player spawns (Setup Passive)
    public override void OnEquip(Mb_CharacterBase user)
    {
        // Debug
        Debug.Log($"{user.name} has equipped {_AbilityData.AbilityName}.");
    }

    // Called when the Player presses the button (Active)
    public override void Activate(Mb_CharacterBase user)
    {
        // Debug
        Debug.Log($"{user.name} has activated {_AbilityData.AbilityName}.");
        if (CheckCooldown( ))   
            CreateProjectile(user);
    }

    private void CreateProjectile(Mb_CharacterBase user)
    {
        if (projectilePrefab != null)
        {
            // Get the projectile origin transform
            Transform originTransform = GameObject.Find("ProjectileOrigin").transform;

            // Get the main camera
            Camera cam = Camera.main;

            // Raycast from the center of the screen
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 targetPoint;

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                targetPoint = hit.point;
            } else
            {
                // If nothing is hit, shoot far into the distance
                targetPoint = ray.origin + ray.direction * 100f;
            }

            // Calculate direction from origin to target
            Vector3 direction = ( targetPoint - originTransform.position ).normalized;

            // Set rotation to look at the target point
            Quaternion rotation = Quaternion.LookRotation(direction);

            // Instantiate the projectile at the origin, facing the target
            GameObject projectileInstance = GameObject.Instantiate(projectilePrefab, originTransform.position, rotation);

            Mb_Projectile mb_Projectile = projectileInstance.GetComponent<Mb_Projectile>( );
            if (mb_Projectile != null)
            {
                mb_Projectile.SetDamageAmount(user.AttackPower.Value( ) * _AbilityData.ATKScaling);
            }
        }

        StartCooldown(user);
    }


    // Called when Character dies (Cleanup)
    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} has unequipped {_AbilityData.AbilityName}.");
    }
}
