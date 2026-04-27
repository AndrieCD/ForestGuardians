// Rajah_R_Branch2.cs
// [R] Eagle Eye — Branch 2 of Rajah Bagwis's ultimate.
//
// PASSIVE (always active after branch is selected):
//   RMB fires additional feathers based on current ability level:
//     Level  1–3  : +0 bonus feathers (base Secondary behavior unchanged)
//     Level  4–7  : +1 bonus feather per shot
//     Level  8–11 : +2 bonus feathers per shot
//     Level 12+   : +4 bonus feathers per shot
//
//   Bonus feathers use the same projectile prefab as Secondary.
//   They are spawned in a tighter spread alongside the main feather.
//   Each bonus feather deals the same damage as the main shot.
//
// ACTIVE (triggered by pressing R):
//   Continuous feather barrage: fires feathers rapidly toward the cursor
//   for a fixed duration. Higher levels increase feather count or duration.
//
// CURRENT STATE: Passive feather count logic is scaffolded with level thresholds.
//   Active barrage is a stub. Both are marked TODO where implementation is needed.
//
// Inspector setup: Assign the RajahBranch2_EagleEye SO_Ability asset to
// SO_Guardian.AbilityR_Branch2. MaxLevel should be at least 12 so all
// feather tiers are reachable.

using System.Collections;
using UnityEngine;

public class Rajah_R_Branch2 : Sc_BaseAbility
{
    // Cached at OnEquip so bonus feathers spawn from the same origin as Secondary
    private Transform _projectileOrigin;

    // Cached at OnEquip so bonus feathers match the Secondary's projectile visually
    private GameObject _projectilePrefab;


    public Rajah_R_Branch2(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        // Cache the projectile origin — same object Secondary uses
        GameObject originObj = GameObject.Find("ProjectileOrigin");
        if (originObj != null)
            _projectileOrigin = originObj.transform;
        else
            Debug.LogError("[Eagle Eye] 'ProjectileOrigin' not found. Bonus feathers won't spawn.");

        // Cache the projectile prefab from this branch's SO
        // Both branches share the same feather visual, so this is consistent with Secondary
        _projectilePrefab = _AbilityData.ProjectileModel;

        // Subscribe to Secondary's fire event — bonus feathers piggyback on each RMB shot
        //Rajah_Secondary.OnSecondaryFired += HandleSecondaryFired;

        Debug.Log($"[{user.name}] Eagle Eye equipped — passive active.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        //Rajah_Secondary.OnSecondaryFired -= HandleSecondaryFired;

        Debug.Log($"[{user.name}] Eagle Eye unequipped.");
    }


    // -------------------------------------------------------------------------
    // Passive — bonus feathers on RMB
    // -------------------------------------------------------------------------

    // Called every time Rajah_Secondary successfully fires
    private void HandleSecondaryFired()
    {
        int bonusCount = GetBonusFeatherCount();

        // No bonus feathers at this level — nothing to do
        if (bonusCount <= 0) return;

        SpawnBonusFeathers(bonusCount);
    }


    // Returns how many extra feathers to add based on current ability level.
    // Thresholds: Level 4 = +1, Level 8 = +2, Level 12 = +4
    private int GetBonusFeatherCount()
    {
        // Read from the SO's "BonusFeatherCount" scaling entry.
        // BaseValuePerLevel array should be set in the Inspector like:
        //   Index: 0  1  2  3  4  5  6  7  8  9  10  11
        //   Value: 0  0  0  1  1  1  1  2  2  2   2   4
        // (Index 0 = Level 1, Index 3 = Level 4, etc.)
        return Mathf.FloorToInt(
            _AbilityData.GetStat("BonusFeatherCount", CurrentLevel)
        );

        // If you prefer to keep thresholds in code instead of the SO,
        // replace the above with this hardcoded version:
        // if (CurrentLevel >= 12) return 4;
        // if (CurrentLevel >= 8)  return 2;
        // if (CurrentLevel >= 4)  return 1;
        // return 0;
    }


    // Spawns bonus feathers in a tight spread alongside the main Secondary shot.
    // Reuses the same aim logic as Secondary — raycasts from screen center.
    private void SpawnBonusFeathers(int count)
    {
        if (_projectilePrefab == null || _projectileOrigin == null) return;

        Camera cam = Camera.main;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int layerMask = ~(1 << LayerMask.NameToLayer("Character"));

        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask)
            ? hit.point
            : ray.origin + ray.direction * 100f;

        Vector3 centerDir = (targetPoint - _projectileOrigin.position).normalized;

        // Tight spread so bonus feathers cluster near the main shot
        // TODO: Expose spread angle in SO if tuning is needed
        float spreadAngle = 8f;

        GameObject[] spawned = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            // Distribute evenly across the tight spread cone
            float t = (count == 1) ? 0.5f : (float)i / (count - 1);
            float angleOffset = Mathf.Lerp(-spreadAngle / 2f, spreadAngle / 2f, t);
            Vector3 dir = Quaternion.AngleAxis(angleOffset, Vector3.up) * centerDir;

            spawned[i] = GameObject.Instantiate(
                _projectilePrefab,
                _projectileOrigin.position,
                Quaternion.LookRotation(dir)
            );

            Mb_Projectile projectile = spawned[i].GetComponent<Mb_Projectile>();
            if (projectile != null)
            {
                // Bonus feathers deal the same damage as a standard Secondary shot
                // TODO: Add a separate "BonusFeatherDamage" scaling entry to the SO
                //       if bonus feathers should scale differently from the main shot
                float damage = _AbilityData.GetStat(
                    "Damage", CurrentLevel,
                    _User.Stats.AttackPower.GetValue()
                );
                projectile.SetDamageAmount(damage);
            }
        }

        // Prevent bonus feathers from colliding with each other mid-flight
        for (int i = 0; i < spawned.Length; i++)
        {
            for (int j = i + 1; j < spawned.Length; j++)
            {
                Collider a = spawned[i]?.GetComponent<Collider>();
                Collider b = spawned[j]?.GetComponent<Collider>();
                if (a != null && b != null)
                    Physics.IgnoreCollision(a, b);
            }
        }
    }


    // -------------------------------------------------------------------------
    // Active — continuous feather barrage
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        TriggerAbilityAnimation(user);
        user.StartCoroutine(BarrageRoutine(user));

        StartCooldown(user, GetAbilityCooldown(user));
    }


    // Fires feathers rapidly toward the cursor for a fixed duration.
    // Duration and fire rate will come from the SO once scaling is filled in.
    private IEnumerator BarrageRoutine(Mb_CharacterBase user)
    {
        // TODO: Pull duration and fire interval from SO scaling entries
        float duration = 3f;   // How long the barrage lasts
        float fireInterval = 0.1f; // Time between each feather

        float elapsed = 0f;

        Debug.Log("[Eagle Eye] Barrage started.");

        while (elapsed < duration)
        {
            FireBarrageFeather(user);

            yield return new WaitForSeconds(fireInterval);
            elapsed += fireInterval;
        }

        Debug.Log("[Eagle Eye] Barrage ended.");
    }


    // Fires one feather toward the cursor during the barrage.
    // Identical aim logic to Secondary — raycast from screen center.
    private void FireBarrageFeather(Mb_CharacterBase user)
    {
        if (_projectilePrefab == null || _projectileOrigin == null) return;

        Camera cam = Camera.main;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int layerMask = ~(1 << LayerMask.NameToLayer("Character"));

        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask)
            ? hit.point
            : ray.origin + ray.direction * 100f;

        Vector3 dir = (targetPoint - _projectileOrigin.position).normalized;
        GameObject instance = GameObject.Instantiate(
            _projectilePrefab,
            _projectileOrigin.position,
            Quaternion.LookRotation(dir)
        );

        Mb_Projectile projectile = instance.GetComponent<Mb_Projectile>();
        if (projectile != null)
        {
            // TODO: Add "BarrageDamage" scaling entry to SO — barrage feathers
            //       may deal less damage than regular shots to balance the fire rate
            float damage = _AbilityData.GetStat(
                "Damage", CurrentLevel,
                user.Stats.AttackPower.GetValue()
            );
            projectile.SetDamageAmount(damage);
        }
    }


    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        // TODO: Add TriggerRAbility() to Mb_GuardianAnimator when animation is ready
        // if (user is Mb_GuardianBase guardian)
        //     guardian.GuardianAnimator?.TriggerRAbility();
    }
}