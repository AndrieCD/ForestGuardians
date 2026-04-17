// Rajah_Secondary.cs
// [RMB] Feather Shot — fires a single feather projectile toward the crosshair.
// Uses AttackSpeed for cooldown (basic attack, not an ability cooldown).
// Scales damage with ATK at current ability level.
//
// Fires Passive_Ability.OnBasicAttackHit on each shot so Royal Plumage can grant stacks.

using UnityEngine;

public class Rajah_Secondary : Sc_BaseAbility
{
    private GameObject _projectilePrefab;

    // Cached once in OnEquip — avoids a scene search every time the ability fires
    private Transform _projectileOrigin;


    public Rajah_Secondary(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
        _projectilePrefab = _AbilityData.ProjectileModel;
    }


    public override void OnEquip(Mb_CharacterBase user)
    {
        GameObject originObj = GameObject.Find("ProjectileOrigin");
        if (originObj != null)
            _projectileOrigin = originObj.transform;
        else
            Debug.LogError("[Rajah_Secondary] 'ProjectileOrigin' GameObject not found in scene.");

        Debug.Log($"[{user.name}] Equipped {_AbilityData.AbilityName}.");
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Unequipped {_AbilityData.AbilityName}.");
    }


    // Called when the player presses [RMB]
    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        FireProjectile(user);
        TriggerAbilityAnimation(user);

        // Secondary is a basic attack — cooldown driven by AttackSpeed, not Haste
        StartCooldown(user, GetAttackCooldown(user));
    }


    private void FireProjectile(Mb_CharacterBase user)
    {
        if (_projectilePrefab == null)
        {
            Debug.LogError("[Rajah_Secondary] No projectile prefab assigned in SO_Ability.");
            return;
        }

        if (_projectileOrigin == null)
        {
            Debug.LogError("[Rajah_Secondary] ProjectileOrigin transform is not cached.");
            return;
        }

        Camera cam = Camera.main;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int layerMask = ~(1 << LayerMask.NameToLayer("Character"));

        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask)
            ? hit.point
            : ray.origin + ray.direction * 100f;

        Vector3 direction = (targetPoint - _projectileOrigin.position).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction);

        GameObject instance = GameObject.Instantiate(_projectilePrefab, _projectileOrigin.position, rotation);

        Mb_Projectile projectile = instance.GetComponent<Mb_Projectile>();
        if (projectile != null)
        {
            float damage = _AbilityData.GetStat("Damage", CurrentLevel, user.Stats.AttackPower.GetValue());
            damage = ApplyCriticalStrike(damage, user);
            projectile.SetDamageAmount(damage);

            // Notify the passive that a basic attack was fired — stack logic lives there
            Passive_Ability.RaiseBasicAttackHit();
        }
    }


    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerSecondaryAttack();
    }
}