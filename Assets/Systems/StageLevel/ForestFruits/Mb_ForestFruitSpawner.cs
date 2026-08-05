using System.Collections.Generic;
using UnityEngine;

public class Mb_ForestFruitSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Mb_RewardsManager _RewardsManager;
    [SerializeField] private GameObject _FruitPrefab;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> _SpawnPoints = new List<Transform>();

    [Header("Spawn Timing")]
    [SerializeField] private bool SpawnOnStageStart = true;
    [SerializeField] private float FirstSpawnDelay = 30f;
    [SerializeField] private float SpawnInterval = 60f;
    [SerializeField] private int MaxActiveFruits = 1;

    [Header("Placeholder Fruit")]
    [SerializeField] private float PlaceholderScale = 0.75f;
    [SerializeField] private Color PlaceholderColor = new Color(1f, 0.28f, 0.18f);

    private readonly List<Mb_ForestFruitCollectible> _activeFruits = new List<Mb_ForestFruitCollectible>();
    private float _spawnTimer;
    private bool _stageRunning;

    private void OnEnable()
    {
        Mb_StageManager.OnStageStart += HandleStageStart;
        Mb_StageManager.OnStageEnd += HandleStageEnd;
    }

    private void OnDisable()
    {
        Mb_StageManager.OnStageStart -= HandleStageStart;
        Mb_StageManager.OnStageEnd -= HandleStageEnd;
    }

    private void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            HandleStageStart();
    }

    private void Update()
    {
        if (!_stageRunning) return;
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing) return;
        if (_SpawnPoints == null || _SpawnPoints.Count == 0) return;
        if (GetActiveFruitCount() >= MaxActiveFruits) return;

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;

        SpawnFruit();
        _spawnTimer = Mathf.Max(1f, SpawnInterval);
    }

    public void NotifyFruitCollected(Mb_ForestFruitCollectible fruit)
    {
        _activeFruits.Remove(fruit);
    }

    private void HandleStageStart()
    {
        _stageRunning = true;
        _spawnTimer = Mathf.Max(0f, SpawnOnStageStart ? FirstSpawnDelay : SpawnInterval);
    }

    private void HandleStageEnd()
    {
        _stageRunning = false;
        ClearActiveFruits();
    }

    private void SpawnFruit()
    {
        Transform spawnPoint = PickSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogWarning("[Mb_ForestFruitSpawner] No valid spawn point found.");
            return;
        }

        GameObject fruit = _FruitPrefab != null
            ? Instantiate(_FruitPrefab, spawnPoint.position, spawnPoint.rotation, transform)
            : CreatePlaceholderFruit(spawnPoint);

        Mb_ForestFruitCollectible collectible = fruit.GetComponent<Mb_ForestFruitCollectible>();
        if (collectible == null)
            collectible = fruit.AddComponent<Mb_ForestFruitCollectible>();

        EnsureTriggerPhysics(fruit);

        collectible.Initialize(_RewardsManager, this);
        _activeFruits.Add(collectible);
    }

    private Transform PickSpawnPoint()
    {
        List<Transform> validSpawnPoints = new List<Transform>();

        foreach (Transform spawnPoint in _SpawnPoints)
        {
            if (spawnPoint != null)
                validSpawnPoints.Add(spawnPoint);
        }

        if (validSpawnPoints.Count == 0)
            return null;

        return validSpawnPoints[Random.Range(0, validSpawnPoints.Count)];
    }

    private GameObject CreatePlaceholderFruit(Transform spawnPoint)
    {
        GameObject fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fruit.name = "Forest Fruit";
        fruit.transform.SetParent(transform);
        fruit.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        fruit.transform.localScale = Vector3.one * Mathf.Max(0.1f, PlaceholderScale);

        Renderer renderer = fruit.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = PlaceholderColor;

        return fruit;
    }

    private void EnsureTriggerPhysics(GameObject fruit)
    {
        Collider fruitCollider = fruit.GetComponent<Collider>();
        if (fruitCollider == null)
            fruitCollider = fruit.AddComponent<SphereCollider>();

        fruitCollider.isTrigger = true;

        Rigidbody rigidbody = fruit.GetComponent<Rigidbody>();
        if (rigidbody == null)
            rigidbody = fruit.AddComponent<Rigidbody>();

        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
    }

    private int GetActiveFruitCount()
    {
        for (int i = _activeFruits.Count - 1; i >= 0; i--)
        {
            if (_activeFruits[i] == null)
                _activeFruits.RemoveAt(i);
        }

        return _activeFruits.Count;
    }

    private void ClearActiveFruits()
    {
        foreach (Mb_ForestFruitCollectible fruit in _activeFruits)
        {
            if (fruit != null)
                Destroy(fruit.gameObject);
        }

        _activeFruits.Clear();
    }

    private void OnValidate()
    {
        FirstSpawnDelay = Mathf.Max(0f, FirstSpawnDelay);
        SpawnInterval = Mathf.Max(1f, SpawnInterval);
        MaxActiveFruits = Mathf.Max(1, MaxActiveFruits);
        PlaceholderScale = Mathf.Max(0.1f, PlaceholderScale);
    }
}
