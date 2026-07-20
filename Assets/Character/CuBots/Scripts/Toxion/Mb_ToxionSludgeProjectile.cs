using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime projectile for Toxion's sludge shot.
/// </summary>
public class Mb_ToxionSludgeProjectile : MonoBehaviour
{
    private const float SLUDGE_OVERLAP_CHECK_HEIGHT = 1.0f;

    private Mb_CharacterBase _owner;
    private float _impactDamage;
    private float _sludgeDamagePerTick;
    private float _slowPercent;
    private float _zoneDuration;
    private float _zoneRadius;
    private float _tickInterval;
    private GameObject _sludgeZonePrefab;
    private bool _hasImpacted;

    public void Initialize(
        Mb_CharacterBase owner,
        float impactDamage,
        float sludgeDamagePerTick,
        float slowPercent,
        float zoneDuration,
        float zoneRadius,
        float tickInterval,
        float projectileLifetime,
        GameObject sludgeZonePrefab)
    {
        _owner = owner;
        _impactDamage = impactDamage;
        _sludgeDamagePerTick = sludgeDamagePerTick;
        _slowPercent = slowPercent;
        _zoneDuration = zoneDuration;
        _zoneRadius = zoneRadius;
        _tickInterval = tickInterval;
        _sludgeZonePrefab = sludgeZonePrefab;
        _hasImpacted = false;

        StartCoroutine(LifetimeFallback(projectileLifetime));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasImpacted) return;
        if (_owner != null && other.gameObject == _owner.gameObject) return;
        if (other.CompareTag("CuBot")) return;
        if (IsAreaEffect(other.gameObject)) return;

        Vector3 impactPoint = other.ClosestPoint(transform.position);
        if (impactPoint == Vector3.zero)
            impactPoint = transform.position;

        I_Damageable damageable = other.GetComponent<I_Damageable>();
        if (damageable != null)
            damageable.TakeDamage(_impactDamage);

        SpawnSludgeZone(impactPoint);
    }

    private IEnumerator LifetimeFallback(float projectileLifetime)
    {
        yield return new WaitForSeconds(projectileLifetime);

        if (!_hasImpacted)
            SpawnSludgeZone(transform.position);
    }

    private void SpawnSludgeZone(Vector3 position)
    {
        _hasImpacted = true;

        if (_sludgeZonePrefab == null)
        {
            Debug.LogError("[Mb_ToxionSludgeProjectile] Cannot spawn sludge zone because no Toxion_SludgeZone prefab was assigned.");
            Destroy(gameObject);
            return;
        }

        if (IsOverlappingSludgeZone(position))
        {
            Destroy(gameObject);
            return;
        }

        GameObject zone = Instantiate(_sludgeZonePrefab);
        zone.name = "Toxion Sludge Zone";
        zone.transform.position = position;
        zone.transform.localScale = new Vector3(_zoneRadius * 2.0f, 0.1f, _zoneRadius * 2.0f);

        int areaEffectLayer = LayerMask.NameToLayer(Mb_ToxionSludgeZone.AREA_EFFECT_LAYER_NAME);
        if (areaEffectLayer >= 0)
            zone.layer = areaEffectLayer;

        Collider zoneCollider = zone.GetComponentInChildren<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        Rigidbody zoneBody = zone.GetComponent<Rigidbody>();
        if (zoneBody == null)
            zoneBody = zone.AddComponent<Rigidbody>();

        zoneBody.isKinematic = true;
        zoneBody.useGravity = false;

        Mb_ToxionSludgeZone sludgeZone = zone.GetComponent<Mb_ToxionSludgeZone>();
        if (sludgeZone == null)
            sludgeZone = zone.AddComponent<Mb_ToxionSludgeZone>();

        sludgeZone.Initialize(
            _sludgeDamagePerTick,
            _slowPercent,
            _zoneDuration,
            _tickInterval
        );

        Destroy(gameObject);
    }

    private bool IsOverlappingSludgeZone(Vector3 position)
    {
        int areaEffectLayer = LayerMask.NameToLayer(Mb_ToxionSludgeZone.AREA_EFFECT_LAYER_NAME);
        if (areaEffectLayer < 0) return false;

        int areaEffectMask = 1 << areaEffectLayer;
        Collider[] overlaps = Physics.OverlapBox(
            position,
            new Vector3(_zoneRadius, SLUDGE_OVERLAP_CHECK_HEIGHT * 0.5f, _zoneRadius),
            Quaternion.identity,
            areaEffectMask,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider overlap in overlaps)
        {
            if (overlap != null && overlap.GetComponentInParent<Mb_ToxionSludgeZone>() != null)
                return true;
        }

        return false;
    }

    private bool IsAreaEffect(GameObject target)
    {
        int areaEffectLayer = LayerMask.NameToLayer(Mb_ToxionSludgeZone.AREA_EFFECT_LAYER_NAME);
        return areaEffectLayer >= 0 && target.layer == areaEffectLayer;
    }
}
