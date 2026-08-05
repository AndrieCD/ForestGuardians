using System.Collections.Generic;
using UnityEngine;

public class Mb_ForestBlessingSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Use the Forest Fruit visual prefab here until Blessings receive unique art.")]
    [SerializeField] private GameObject _BlessingPrefab;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> _SpawnPoints = new List<Transform>();

    [Header("Natural Spawn Timing")]
    [SerializeField] private bool SpawnNaturally = true;
    [SerializeField] private float FirstNaturalSpawnDelay = 45f;
    [SerializeField] private float NaturalSpawnInterval = 75f;
    [SerializeField] private int MaxActiveNaturalBlessings = 2;

    [Header("Elite Drops")]
    [SerializeField] private bool SpawnFromEliteDeaths = true;

    [Header("Despawn")]
    [SerializeField] private float DespawnDuration = 10f;

    [Header("Placeholder Blessing")]
    [SerializeField] private float PlaceholderScale = 0.75f;
    [SerializeField] private Color PlaceholderColor = new Color(0.25f, 1f, 0.55f);

    [Header("Blessings")]
    [SerializeField] private List<Sc_ForestBlessingData> _Blessings = new List<Sc_ForestBlessingData>
    {
        new Sc_ForestBlessingData(
            "Storm Blessing",
            5f,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.MoveSpeed, 0.35f, StatModType.Percent),
                new Sc_StatEffect(StatType.AttackSpeed, 0.35f, StatModType.Percent)
            }
        ),
        new Sc_ForestBlessingData(
            "Nature's Flow",
            5f,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.Haste, 150f, StatModType.Flat)
            }
        ),
        new Sc_ForestBlessingData(
            "Wrath of the Forest",
            4f,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.AttackPower, 0.75f, StatModType.Percent),
                new Sc_StatEffect(StatType.AbilityPower, 0.75f, StatModType.Percent)
            }
        ),
        new Sc_ForestBlessingData
        {
            BlessingName = "Guardian's Vitality",
            EffectDuration = 6f,
            StatEffects = new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.HealthRegen, 40f, StatModType.Flat)
            },
            ShieldMaxHealthPercent = 0.35f
        }
    };

    private readonly List<Mb_ForestBlessingCollectible> _activeNaturalBlessings = new List<Mb_ForestBlessingCollectible>();
    private readonly List<Mb_ForestBlessingCollectible> _activeEliteBlessings = new List<Mb_ForestBlessingCollectible>();
    private float _naturalSpawnTimer;
    private bool _stageRunning;

    private void OnEnable()
    {
        Mb_StageManager.OnStageStart += HandleStageStart;
        Mb_StageManager.OnStageEnd += HandleStageEnd;
        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath;
    }

    private void OnDisable()
    {
        Mb_StageManager.OnStageStart -= HandleStageStart;
        Mb_StageManager.OnStageEnd -= HandleStageEnd;
        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;
    }

    private void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            HandleStageStart();
    }

    private void Update()
    {
        if (!SpawnNaturally) return;
        if (!_stageRunning) return;
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing) return;
        if (_SpawnPoints == null || _SpawnPoints.Count == 0) return;
        if (GetActiveNaturalBlessingCount() >= MaxActiveNaturalBlessings) return;

        _naturalSpawnTimer -= Time.deltaTime;
        if (_naturalSpawnTimer > 0f) return;

        SpawnNaturalBlessing();
        _naturalSpawnTimer = Mathf.Max(1f, NaturalSpawnInterval);
    }

    public void NotifyBlessingRemoved(Mb_ForestBlessingCollectible blessing)
    {
        _activeNaturalBlessings.Remove(blessing);
        _activeEliteBlessings.Remove(blessing);
    }

    private void HandleStageStart()
    {
        _stageRunning = true;
        _naturalSpawnTimer = Mathf.Max(0f, FirstNaturalSpawnDelay);
    }

    private void HandleStageEnd()
    {
        _stageRunning = false;
        ClearActiveBlessings();
    }

    private void HandleCuBotDeath(GameObject deadCuBot)
    {
        if (!SpawnFromEliteDeaths) return;
        if (!_stageRunning) return;
        if (deadCuBot == null) return;

        Mb_EliteCuBot eliteCuBot = deadCuBot.GetComponent<Mb_EliteCuBot>();
        if (eliteCuBot == null || !eliteCuBot.IsElite) return;

        SpawnBlessing(deadCuBot.transform.position, deadCuBot.transform.rotation, false);
    }

    private void SpawnNaturalBlessing()
    {
        Transform spawnPoint = PickSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogWarning("[Mb_ForestBlessingSpawner] No valid spawn point found.");
            return;
        }

        SpawnBlessing(spawnPoint.position, spawnPoint.rotation, true);
    }

    private void SpawnBlessing(Vector3 position, Quaternion rotation, bool isNaturalSpawn)
    {
        Sc_ForestBlessingData blessingData = PickBlessing();
        if (blessingData == null)
        {
            Debug.LogWarning("[Mb_ForestBlessingSpawner] No valid Blessing data found.");
            return;
        }

        GameObject blessingObject = _BlessingPrefab != null
            ? Instantiate(_BlessingPrefab, position, rotation, transform)
            : CreatePlaceholderBlessing(position, rotation);

        blessingObject.name = blessingData.BlessingName;

        DisableForestFruitCollectible(blessingObject);
        EnsureTriggerPhysics(blessingObject);

        Mb_ForestBlessingCollectible collectible = blessingObject.GetComponent<Mb_ForestBlessingCollectible>();
        if (collectible == null)
            collectible = blessingObject.AddComponent<Mb_ForestBlessingCollectible>();

        collectible.Initialize(blessingData, this, DespawnDuration);

        if (isNaturalSpawn)
            _activeNaturalBlessings.Add(collectible);
        else
            _activeEliteBlessings.Add(collectible);
    }

    private Sc_ForestBlessingData PickBlessing()
    {
        List<Sc_ForestBlessingData> validBlessings = new List<Sc_ForestBlessingData>();

        foreach (Sc_ForestBlessingData blessing in _Blessings)
        {
            if (blessing != null)
                validBlessings.Add(blessing);
        }

        if (validBlessings.Count == 0)
            return null;

        return validBlessings[Random.Range(0, validBlessings.Count)];
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

    private GameObject CreatePlaceholderBlessing(Vector3 position, Quaternion rotation)
    {
        GameObject blessing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blessing.transform.SetParent(transform);
        blessing.transform.SetPositionAndRotation(position, rotation);
        blessing.transform.localScale = Vector3.one * Mathf.Max(0.1f, PlaceholderScale);

        Renderer renderer = blessing.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = PlaceholderColor;

        return blessing;
    }

    private void DisableForestFruitCollectible(GameObject blessingObject)
    {
        Mb_ForestFruitCollectible fruitCollectible = blessingObject.GetComponent<Mb_ForestFruitCollectible>();
        if (fruitCollectible != null)
            fruitCollectible.enabled = false;
    }

    private void EnsureTriggerPhysics(GameObject blessingObject)
    {
        Collider blessingCollider = blessingObject.GetComponent<Collider>();
        if (blessingCollider == null)
            blessingCollider = blessingObject.AddComponent<SphereCollider>();

        blessingCollider.isTrigger = true;

        Rigidbody rigidbody = blessingObject.GetComponent<Rigidbody>();
        if (rigidbody == null)
            rigidbody = blessingObject.AddComponent<Rigidbody>();

        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
    }

    private int GetActiveNaturalBlessingCount()
    {
        for (int i = _activeNaturalBlessings.Count - 1; i >= 0; i--)
        {
            if (_activeNaturalBlessings[i] == null)
                _activeNaturalBlessings.RemoveAt(i);
        }

        return _activeNaturalBlessings.Count;
    }

    private void ClearActiveBlessings()
    {
        DestroyBlessings(_activeNaturalBlessings);
        DestroyBlessings(_activeEliteBlessings);

        _activeNaturalBlessings.Clear();
        _activeEliteBlessings.Clear();
    }

    private void DestroyBlessings(List<Mb_ForestBlessingCollectible> blessings)
    {
        foreach (Mb_ForestBlessingCollectible blessing in blessings)
        {
            if (blessing != null)
                Destroy(blessing.gameObject);
        }
    }

    private void OnValidate()
    {
        FirstNaturalSpawnDelay = Mathf.Max(0f, FirstNaturalSpawnDelay);
        NaturalSpawnInterval = Mathf.Max(1f, NaturalSpawnInterval);
        MaxActiveNaturalBlessings = Mathf.Max(1, MaxActiveNaturalBlessings);
        DespawnDuration = Mathf.Max(0.1f, DespawnDuration);
        PlaceholderScale = Mathf.Max(0.1f, PlaceholderScale);
    }
}
