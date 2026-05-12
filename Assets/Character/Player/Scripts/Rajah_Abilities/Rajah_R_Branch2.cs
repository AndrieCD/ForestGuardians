using System.Collections;
using UnityEngine;

public class Rajah_R_Branch2 : Sc_BaseAbility
{
    // -------------------------
    // Passive Config
    // -------------------------
    private float _extraShotDelay = 0.2f;
    private const float PASSIVE_SPREAD_RADIUS = 0.5f;

    private Coroutine _passiveRoutine;

    // -------------------------
    // Active Config
    // -------------------------
    private const float ACTIVE_DURATION = 5f;
    private const float FIRE_INTERVAL = 0.1f; // 10/sec
    private const float SPREAD_ANGLE = 15f;
    private const float ACTIVE_SPREAD_RADIUS = 1f;

    //private Transform _Guardian.ProjectileOrigin;
    private GameObject _projectilePrefab;

    public Rajah_R_Branch2(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
        _projectilePrefab = _AbilityData.ProjectileModel;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        //GameObject originObj = GameObject.Find("ProjectileOrigin");
        //if (originObj != null)
        //    _Guardian.ProjectileOrigin = originObj.transform;

        Rajah_Secondary.OnSecondaryFired += HandleSecondaryFired;
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Rajah_Secondary.OnSecondaryFired -= HandleSecondaryFired;
    }

    // -------------------------------------------------------------------------
    // PASSIVE
    // -------------------------------------------------------------------------

    private void HandleSecondaryFired(Mb_CharacterBase source, Vector3 origin, Vector3 direction)
    {
        if (source != _User) return;

        int extraShots = GetExtraShotCount();

        if (_passiveRoutine != null)
            _User.StopCoroutine(_passiveRoutine);

        _passiveRoutine = _User.StartCoroutine(FireExtraShots(origin, direction, extraShots));
    }

    private IEnumerator FireExtraShots(Vector3 origin, Vector3 direction, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(_extraShotDelay);
            SpawnProjectile(origin, direction, isPassive: true);
        }
    }

    private int GetExtraShotCount()
    {
        int level = _User.GetLevel(); // or wherever level is stored

        if (level >= 12) return 4;
        if (level >= 8) return 2;
        if (level >= 4) return 1;
        return 0;
    }

    // -------------------------------------------------------------------------
    // ACTIVE
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Activated {_AbilityData.name}.");
        if (!CheckCooldown()) return;

        var controller = user as Mb_PlayerController;

        controller?.AddDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );

        user.StartCoroutine(ActiveRoutine(user, controller));

        StartCooldown(user, GetAbilityCooldown(user));
    }

    private IEnumerator ActiveRoutine(Mb_CharacterBase user, Mb_PlayerController controller)
    {
        float elapsed = 0f;

        while (elapsed < ACTIVE_DURATION)
        {
            FireSpreadShot(user, isPassive: false);
            yield return new WaitForSeconds(FIRE_INTERVAL);
            elapsed += FIRE_INTERVAL;
        }

        controller?.RemoveDisable(
            ActionDisableFlags.AllAbilities |
            ActionDisableFlags.AllAttacks
        );
    }

    private void FireSpreadShot(Mb_CharacterBase user, bool isPassive)
    {
        if (_Guardian.ProjectileOrigin == null)
            return;

        Camera cam = Camera.main;

        Ray ray = cam.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        int layerMask = ~(1 << LayerMask.NameToLayer("Character"));

        Vector3 targetPoint =
            Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask)
            ? hit.point
            : ray.origin + ray.direction * 100f;

        SpawnProjectile(
            _Guardian.ProjectileOrigin.position,
            targetPoint,
            isPassive
        );
    }

    // -------------------------------------------------------------------------
    // PROJECTILE SPAWNING (SHARED)
    // -------------------------------------------------------------------------

    private void SpawnProjectile(
    Vector3 origin,
    Vector3 targetPoint,
    bool isPassive
)
    {
        if (_projectilePrefab == null)
            return;

        // ---------------------------------------------------------------------
        // Select spread radius
        // ---------------------------------------------------------------------

        float spreadRadius = isPassive
            ? PASSIVE_SPREAD_RADIUS
            : ACTIVE_SPREAD_RADIUS;

        // ---------------------------------------------------------------------
        // Random local offset around projectile origin
        // ---------------------------------------------------------------------

        Vector2 randomCircle =
            Random.insideUnitCircle * spreadRadius; // Random point in circle for spread

        Vector3 offset =
            (_Guardian.transform.right * randomCircle.x) +
            (_Guardian.transform.up * randomCircle.y);  

        Vector3 spawnPosition = origin + offset;

        // ---------------------------------------------------------------------
        // Recalculate direction AFTER offset
        // ---------------------------------------------------------------------

        Vector3 direction =
            (targetPoint - spawnPosition).normalized;

        Quaternion rotation =
            Quaternion.LookRotation(direction);

        // ---------------------------------------------------------------------
        // Spawn projectile
        // ---------------------------------------------------------------------

        GameObject instance = GameObject.Instantiate(
            _projectilePrefab,
            spawnPosition,
            rotation
        );

        Mb_Projectile proj =
            instance.GetComponent<Mb_Projectile>();

        if (proj != null)
        {
            float damage = _AbilityData.GetStat(
                "Damage",
                CurrentLevel,
                _User.Stats.AttackPower.GetValue()
            );

            proj.SetOwner(_User);
            proj.SetOwnerTag("Player");
            proj.SetDamageAmount(damage);

            // ❌ No crit
            // ❌ No passive triggers
        }
    }

    // -------------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------------

}