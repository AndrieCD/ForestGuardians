using System.Collections;
using UnityEngine;

/// <summary>
/// Stage event spawner that periodically drops physics-driven damaging rocks.
/// Attach this to a scene GameObject and assign one or more rock prefabs.
/// </summary>
public class Mb_FallingRocksEvent : MonoBehaviour
{
    [Header("Rock Prefabs")]
    [Tooltip("Rock prefab variants to spawn. Each spawned rock receives Mb_FallingRockHazard if missing.")]
    [SerializeField] private GameObject[] RockPrefabs;

    [Header("Timing")]
    [Tooltip("Minimum seconds before the next falling rocks event.")]
    [SerializeField] private float MinEventInterval = 20f;

    [Tooltip("Maximum seconds before the next falling rocks event.")]
    [SerializeField] private float MaxEventInterval = 60f;

    [Header("Spawn Area")]
    [Tooltip("Optional center point for the spawn area. Uses this GameObject's transform when empty.")]
    [SerializeField] private Transform SpawnCenter;

    [Tooltip("Horizontal radius around SpawnCenter where rocks can spawn.")]
    [SerializeField] private float SpawnRadius = 20f;

    [Tooltip("Height above SpawnCenter where rocks are instantiated.")]
    [SerializeField] private float SpawnHeight = 25f;

    [Tooltip("Minimum rocks spawned per event.")]
    [SerializeField] private int MinRocksPerEvent = 3;

    [Tooltip("Maximum rocks spawned per event.")]
    [SerializeField] private int MaxRocksPerEvent = 6;

    [Tooltip("Seconds over which rocks are spaced out during one event.")]
    [SerializeField] private float SpawnDuration = 1f;

    [Header("Rock Runtime")]
    [Tooltip("Minimum random uniform scale applied to each spawned rock.")]
    [SerializeField] private float MinRockScale = 0.5f;

    [Tooltip("Maximum random uniform scale applied to each spawned rock.")]
    [SerializeField] private float MaxRockScale = 1.1f;

    [Tooltip("Damage dealt by the smallest rock.")]
    [SerializeField] private float MinRockDamage = 15f;

    [Tooltip("Damage dealt by the largest rock.")]
    [SerializeField] private float MaxRockDamage = 35f;

    [Tooltip("Seconds before spawned rocks are destroyed.")]
    [SerializeField] private float RockLifetime = 10f;

    [Tooltip("Random horizontal velocity added when each rock spawns.")]
    [SerializeField] private float RandomHorizontalVelocity = 1.5f;

    [Tooltip("Random torque applied when each rock spawns.")]
    [SerializeField] private float RandomTorque = 8f;

    private Coroutine _eventRoutine;

    private void OnEnable()
    {
        _eventRoutine = StartCoroutine(EventRoutine());
    }

    private void OnDisable()
    {
        if (_eventRoutine == null) return;

        StopCoroutine(_eventRoutine);
        _eventRoutine = null;
    }

    private IEnumerator EventRoutine()
    {
        while (true)
        {
            float interval = Random.Range(MinEventInterval, MaxEventInterval);
            yield return new WaitForSeconds(interval);

            yield return SpawnFallingRocksRoutine();
        }
    }

    [ContextMenu("DEBUG - Spawn Falling Rocks")]
    private void SpawnFallingRocks()
    {
        if (!isActiveAndEnabled) return;

        StartCoroutine(SpawnFallingRocksRoutine());
    }

    private IEnumerator SpawnFallingRocksRoutine()
    {
        if (RockPrefabs == null || RockPrefabs.Length == 0)
        {
            Debug.LogWarning("[Mb_FallingRocksEvent] No RockPrefabs assigned.");
            yield break;
        }

        int rockCount = Random.Range(MinRocksPerEvent, MaxRocksPerEvent + 1);
        float spawnDelay = rockCount > 1 ? SpawnDuration / (rockCount - 1) : 0f;

        for (int i = 0; i < rockCount; i++)
        {
            SpawnRock();

            if (spawnDelay > 0f && i < rockCount - 1)
                yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void SpawnRock()
    {
        GameObject prefab = GetRandomRockPrefab();
        if (prefab == null) return;

        Vector3 position = GetRandomSpawnPosition();
        Quaternion rotation = Random.rotation;

        GameObject rockObject = Instantiate(prefab, position, rotation);
        rockObject.name = $"{prefab.name}_FallingRock";

        float rockScale = Random.Range(MinRockScale, MaxRockScale);
        float rockDamage = GetScaledDamage(rockScale);

        ConfigureRock(rockObject, rockScale, rockDamage);
    }

    private GameObject GetRandomRockPrefab()
    {
        for (int i = 0; i < RockPrefabs.Length; i++)
        {
            GameObject prefab = RockPrefabs[Random.Range(0, RockPrefabs.Length)];
            if (prefab != null)
                return prefab;
        }

        Debug.LogWarning("[Mb_FallingRocksEvent] RockPrefabs contains only null entries.");
        return null;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Transform center = SpawnCenter != null ? SpawnCenter : transform;
        Vector2 offset = Random.insideUnitCircle * SpawnRadius;

        return center.position + new Vector3(offset.x, SpawnHeight, offset.y);
    }

    private void ConfigureRock(GameObject rockObject, float rockScale, float rockDamage)
    {
        rockObject.transform.localScale *= rockScale;

        Rigidbody rigidbody = rockObject.GetComponent<Rigidbody>();
        if (rigidbody == null)
            rigidbody = rockObject.AddComponent<Rigidbody>();

        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.linearVelocity = GetRandomHorizontalVelocity();
        rigidbody.angularVelocity = Random.insideUnitSphere * RandomTorque;

        Collider collider = rockObject.GetComponent<Collider>();
        if (collider == null)
            collider = rockObject.AddComponent<SphereCollider>();

        collider.isTrigger = true;

        Mb_FallingRockHazard hazard = rockObject.GetComponent<Mb_FallingRockHazard>();
        if (hazard == null)
            hazard = rockObject.AddComponent<Mb_FallingRockHazard>();

        hazard.Initialize(rockDamage, RockLifetime);
    }

    private float GetScaledDamage(float rockScale)
    {
        float normalizedScale = Mathf.InverseLerp(MinRockScale, MaxRockScale, rockScale);
        return Mathf.Lerp(MinRockDamage, MaxRockDamage, normalizedScale);
    }

    private Vector3 GetRandomHorizontalVelocity()
    {
        Vector2 velocity = Random.insideUnitCircle * RandomHorizontalVelocity;
        return new Vector3(velocity.x, 0f, velocity.y);
    }
}
