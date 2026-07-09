using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// Mb_LaserBeam.cs
// Owns Mind Unbound's per-frame raycast, damage application, LineRenderer update,
// and hit VFX positioning.
// =============================================================================

public class Mb_LaserBeam : MonoBehaviour
{
    private Mb_CharacterBase _owner;
    private Transform _origin;
    private float _dps;
    private float _tickInterval;
    private float _maxRange;
    private float _radius;
    private LayerMask _layerMask;
    private float _tickTimer;

    private LineRenderer _lineRenderer;
    private ParticleSystem _hitVFX;
    private bool _hitVFXPlaying = false;
    private bool _isInitialized = false;
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[64];
    private readonly HashSet<MB_CuBotBase> _damagedThisFrame = new HashSet<MB_CuBotBase>();

    public void Initialize(
        Mb_CharacterBase owner,
        Transform origin,
        float dps,
        float tickInterval,
        float maxRange,
        float radius,
        LayerMask layerMask)
    {
        _owner = owner;
        _origin = origin;
        _dps = dps;
        _tickInterval = Mathf.Max(0.01f, tickInterval);
        _maxRange = maxRange;
        _radius = Mathf.Max(0.01f, radius);
        _layerMask = layerMask.value == 0 ? Physics.DefaultRaycastLayers : layerMask;
        _tickTimer = _tickInterval;

        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
        {
            Debug.LogError("[Mb_LaserBeam] No LineRenderer found on laser prefab.");
        }
        else
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.startWidth = _radius * 2f;
            _lineRenderer.endWidth = _radius * 2f;
            _lineRenderer.enabled = true;
        }

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "LaserHitVFX")
            {
                _hitVFX = ps;
                break;
            }
        }

        _isInitialized = true;
    }

    public void FireThisFrame()
    {
        if (!_isInitialized || _origin == null) return;

        Vector3 rayOrigin = _origin.position;
        Vector3 rayDirection = ResolveAimDirection(rayOrigin);
        Vector3 endPoint = rayOrigin + rayDirection * _maxRange;
        bool hasHitVFXPoint = false;
        RaycastHit hitVFXPoint = default;
        bool shouldApplyDamage = AdvanceDamageTick();
        _damagedThisFrame.Clear();

        int hitCount = Physics.SphereCastNonAlloc(
            rayOrigin,
            _radius,
            rayDirection,
            _hitBuffer,
            _maxRange,
            _layerMask,
            QueryTriggerInteraction.Collide
        );

        SortHitsByDistance(hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _hitBuffer[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null) continue;

            Mb_CharacterBase character = hitCollider.GetComponentInParent<Mb_CharacterBase>();
            if (character != null && character == _owner)
                continue;

            MB_CuBotBase enemy = character as MB_CuBotBase;
            if (enemy != null)
            {
                if (shouldApplyDamage &&
                    _damagedThisFrame.Add(enemy) &&
                    enemy.Health != null &&
                    !enemy.Health.IsDead)
                {
                    float damageThisTick = _dps * _tickInterval;
                    enemy.Health.TakeDamage(damageThisTick);
                }

                if (!hasHitVFXPoint)
                {
                    hasHitVFXPoint = true;
                    hitVFXPoint = hit;
                }

                continue;
            }

            hasHitVFXPoint = true;
            hitVFXPoint = hit;
            endPoint = hit.point;
            break;
        }

        if (hasHitVFXPoint)
        {
            Vector3 hitNormal = hitVFXPoint.normal.sqrMagnitude > 0.001f
                ? hitVFXPoint.normal
                : -rayDirection;

            PositionHitVFX(hitVFXPoint.point, hitNormal);
        }
        else
        {
            StopHitVFX();
        }

        UpdateBeamVisual(rayOrigin, endPoint);
    }

    private bool AdvanceDamageTick()
    {
        _tickTimer += Time.deltaTime;

        if (_tickTimer < _tickInterval)
            return false;

        _tickTimer -= _tickInterval;
        return true;
    }

    private Vector3 ResolveAimDirection(Vector3 rayOrigin)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return _origin.forward;

        Ray aimRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimTarget = aimRay.GetPoint(_maxRange);
        int aimHitCount = Physics.RaycastNonAlloc(
            aimRay,
            _hitBuffer,
            _maxRange,
            _layerMask,
            QueryTriggerInteraction.Ignore
        );

        SortHitsByDistance(aimHitCount);

        for (int i = 0; i < aimHitCount; i++)
        {
            Collider hitCollider = _hitBuffer[i].collider;
            if (hitCollider == null) continue;

            Mb_CharacterBase character = hitCollider.GetComponentInParent<Mb_CharacterBase>();
            if (character != null && character == _owner)
                continue;

            aimTarget = _hitBuffer[i].point;
            break;
        }

        Vector3 direction = aimTarget - rayOrigin;
        if (direction.sqrMagnitude <= 0.001f)
            return _origin.forward;

        Vector3 normalizedDirection = direction.normalized;
        if (_owner != null && Vector3.Dot(_owner.transform.forward, normalizedDirection) <= 0f)
            return _owner.transform.forward;

        return normalizedDirection;
    }

    private void SortHitsByDistance(int hitCount)
    {
        for (int i = 1; i < hitCount; i++)
        {
            RaycastHit current = _hitBuffer[i];
            int j = i - 1;

            while (j >= 0 && _hitBuffer[j].distance > current.distance)
            {
                _hitBuffer[j + 1] = _hitBuffer[j];
                j--;
            }

            _hitBuffer[j + 1] = current;
        }
    }

    private void UpdateBeamVisual(Vector3 from, Vector3 to)
    {
        if (_lineRenderer == null) return;

        _lineRenderer.SetPosition(0, from);
        _lineRenderer.SetPosition(1, to);
    }

    private void PositionHitVFX(Vector3 position, Vector3 normal)
    {
        if (_hitVFX == null) return;

        _hitVFX.transform.position = position;
        _hitVFX.transform.rotation = Quaternion.LookRotation(normal);

        if (!_hitVFXPlaying)
        {
            _hitVFX.Play();
            _hitVFXPlaying = true;
        }
    }

    private void StopHitVFX()
    {
        if (_hitVFX == null || !_hitVFXPlaying) return;

        _hitVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _hitVFXPlaying = false;
    }

    public void Deactivate()
    {
        _isInitialized = false;

        StopHitVFX();

        if (_lineRenderer != null)
            _lineRenderer.enabled = false;

        Destroy(gameObject, 1.5f);
    }
}
