using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime projectile for Toxion's sludge shot.
/// </summary>
public class Mb_ToxionSludgeProjectile : MonoBehaviour
{
    private Mb_CharacterBase _owner;
    private float _impactDamage;
    private float _sludgeDamagePerTick;
    private float _slowPercent;
    private float _zoneDuration;
    private float _zoneRadius;
    private float _tickInterval;
    private bool _hasImpacted;

    public void Initialize(
        Mb_CharacterBase owner,
        float impactDamage,
        float sludgeDamagePerTick,
        float slowPercent,
        float zoneDuration,
        float zoneRadius,
        float tickInterval,
        float projectileLifetime)
    {
        _owner = owner;
        _impactDamage = impactDamage;
        _sludgeDamagePerTick = sludgeDamagePerTick;
        _slowPercent = slowPercent;
        _zoneDuration = zoneDuration;
        _zoneRadius = zoneRadius;
        _tickInterval = tickInterval;
        _hasImpacted = false;

        StartCoroutine(LifetimeFallback(projectileLifetime));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasImpacted) return;
        if (_owner != null && other.gameObject == _owner.gameObject) return;
        if (other.CompareTag("CuBot")) return;

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

        GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        zone.name = "Toxion Sludge Zone";
        zone.transform.position = position + Vector3.up * 0.05f;
        zone.transform.localScale = new Vector3(_zoneRadius * 2.0f, 0.05f, _zoneRadius * 2.0f);

        Collider zoneCollider = zone.GetComponent<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        Rigidbody zoneBody = zone.AddComponent<Rigidbody>();
        zoneBody.isKinematic = true;
        zoneBody.useGravity = false;

        Mb_ToxionSludgeZone sludgeZone = zone.AddComponent<Mb_ToxionSludgeZone>();
        sludgeZone.Initialize(
            _sludgeDamagePerTick,
            _slowPercent,
            _zoneDuration,
            _tickInterval
        );

        Destroy(gameObject);
    }
}
