using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// Mb_LaserBeam.cs
// Owns Mind Unbound's per-frame beam aiming, trigger-based damage application,
// LineRenderer update, and hit VFX positioning.
// =============================================================================

public class Mb_LaserBeam : MonoBehaviour
{
    private const float EXIT_TRANSITION_DURATION = 0.2f;

    [Header("Visual Beam Animation")]
    [SerializeField, Range(0f, 0.5f)]
    private float _VisualScaleAmplitude = 0.1f;

    [SerializeField, Min(0f)]
    private float _VisualScaleCyclesPerSecond = 3f;

    [SerializeField]
    private float _VisualRotationDegreesPerSecond = 180f;

    private Mb_CharacterBase _owner;
    private Transform _origin;
    private float _dps;
    private float _tickInterval;
    private float _maxRange;
    private float _radius;
    private LayerMask _layerMask;
    private float _tickTimer;

    private LineRenderer _lineRenderer;
    private BoxCollider _beamCollider;
    private Transform _visualBeam;
    private ParticleSystem _hitVFX;
    private bool _hitVFXPlaying = false;
    private bool _isInitialized = false;
    private bool _isExiting;
    private Vector3 _visualBeamBaseScale;
    private Quaternion _visualBeamBaseLocalRotation;
    private float _visualAnimationTime;
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[64];
    private readonly Dictionary<MB_CuBotBase, HashSet<Collider>> _overlappingEnemies =
        new Dictionary<MB_CuBotBase, HashSet<Collider>>();

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

        _visualBeam = transform.Find("VisualBeam");
        if (_visualBeam == null)
            _visualBeam = transform.Find("visualbeam");

        if (_visualBeam != null)
        {
            _visualBeamBaseScale = _visualBeam.localScale;
            _visualBeamBaseLocalRotation = _visualBeam.localRotation;
            _visualAnimationTime = 0f;
        }

        _beamCollider = GetComponent<BoxCollider>();
        if (_beamCollider != null)
        {
            _beamCollider.isTrigger = true;
            _beamCollider.enabled = true;
        }

        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer != null)
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
        UpdateBeamCollider(_maxRange);
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterOverlappingEnemy(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Stay also catches enemies that were already overlapping when the
        // trigger was enabled or when the beam length changed.
        RegisterOverlappingEnemy(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!TryGetEnemy(other, out MB_CuBotBase enemy)) return;

        if (!_overlappingEnemies.TryGetValue(enemy, out HashSet<Collider> colliders))
            return;

        colliders.Remove(other);
        if (colliders.Count == 0)
            _overlappingEnemies.Remove(enemy);
    }

    private void RegisterOverlappingEnemy(Collider other)
    {
        if (!TryGetEnemy(other, out MB_CuBotBase enemy)) return;

        if (!_overlappingEnemies.TryGetValue(enemy, out HashSet<Collider> colliders))
        {
            colliders = new HashSet<Collider>();
            _overlappingEnemies.Add(enemy, colliders);
        }

        colliders.Add(other);
    }

    private bool TryGetEnemy(Collider other, out MB_CuBotBase enemy)
    {
        enemy = null;

        if (!_isInitialized || other == null) return false;

        Mb_CharacterBase character = other.GetComponentInParent<Mb_CharacterBase>();
        enemy = character as MB_CuBotBase;
        return enemy != null && enemy != _owner;
    }

    public void FireThisFrame()
    {
        if (!_isInitialized || _origin == null) return;

        Vector3 rayOrigin = _origin.position;
        Vector3 rayDirection = ResolveAimDirection(rayOrigin);
        transform.rotation = Quaternion.LookRotation(rayDirection, _origin.up);
        UpdateVisualAnimation();
        Vector3 endPoint = rayOrigin + rayDirection * _maxRange;
        bool hasHitVFXPoint = false;
        RaycastHit hitVFXPoint = default;
        bool shouldApplyDamage = AdvanceDamageTick();

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
            if (hitCollider == _beamCollider) continue;

            Mb_CharacterBase character = hitCollider.GetComponentInParent<Mb_CharacterBase>();
            if (character != null && character == _owner)
                continue;

            MB_CuBotBase enemy = character as MB_CuBotBase;
            if (enemy != null)
            {
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
        UpdateBeamCollider(Vector3.Distance(rayOrigin, endPoint));

        if (shouldApplyDamage)
            ApplyDamageTick();
    }

    private void ApplyDamageTick()
    {
        float damageThisTick = _dps * _tickInterval;

        foreach (KeyValuePair<MB_CuBotBase, HashSet<Collider>> entry in _overlappingEnemies)
        {
            MB_CuBotBase enemy = entry.Key;
            if (enemy == null || enemy.Health == null || enemy.Health.IsDead)
                continue;

            enemy.Health.TakeDamage(damageThisTick);
        }
    }

    private void UpdateBeamCollider(float beamLength)
    {
        if (_beamCollider == null) return;

        Vector3 lossyScale = transform.lossyScale;
        float scaleX = Mathf.Max(Mathf.Abs(lossyScale.x), 0.0001f);
        float scaleY = Mathf.Max(Mathf.Abs(lossyScale.y), 0.0001f);
        float scaleZ = Mathf.Max(Mathf.Abs(lossyScale.z), 0.0001f);
        float clampedLength = Mathf.Max(beamLength, 0.01f);

        _beamCollider.size = new Vector3(
            (_radius * 2f) / scaleX,
            (_radius * 2f) / scaleY,
            clampedLength / scaleZ
        );
        _beamCollider.center = new Vector3(
            0f,
            0f,
            (clampedLength * 0.5f) / scaleZ
        );
    }

    private void UpdateVisualAnimation()
    {
        if (_visualBeam == null) return;

        _visualAnimationTime += Time.deltaTime;

        float pulsePhase = _visualAnimationTime * _VisualScaleCyclesPerSecond * Mathf.PI * 2f;
        float scaleMultiplier = 1f + Mathf.Sin(pulsePhase) * _VisualScaleAmplitude;
        _visualBeam.localScale = _visualBeamBaseScale * scaleMultiplier;

        _visualBeam.localRotation *= Quaternion.Euler(
            0f,
            0f,
            _VisualRotationDegreesPerSecond * Time.deltaTime
        );
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

        Ray aimRay = cam.ScreenPointToRay(
            new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
        );
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

        return direction.normalized;
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

        _hitVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _hitVFXPlaying = false;
    }

    public void Deactivate()
    {
        if (_isExiting) return;

        _isExiting = true;
        _isInitialized = false;
        _overlappingEnemies.Clear();

        if (_visualBeam != null)
        {
            _visualBeam.localScale = _visualBeamBaseScale;
            _visualBeam.localRotation = _visualBeamBaseLocalRotation;
        }

        foreach (ParticleSystem particleSystem in GetComponentsInChildren<ParticleSystem>(true))
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (_beamCollider != null)
            _beamCollider.enabled = false;

        if (_hitVFX != null && _hitVFXPlaying)
        {
            _hitVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _hitVFXPlaying = false;
        }

        if (_lineRenderer != null)
            _lineRenderer.enabled = false;

        StartCoroutine(Sc_VFXExitTransition.ScaleDownAndDestroy(
            transform,
            EXIT_TRANSITION_DURATION
        ));
    }
}
