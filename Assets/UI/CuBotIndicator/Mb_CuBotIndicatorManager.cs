// Mb_CuBotIndicatorManager.cs
// Singleton manager that owns and drives all screen-edge CuBot indicators.
//
// HOW IT WORKS:
//   - Lives on a persistent Screen Space — Overlay Canvas in the scene.
//   - Maintains a pool of Mb_CuBotIndicator instances.
//   - Subscribes to MB_CuBotBase.OnCuBotSpawn and OnCuBotDeath(GameObject).
//   - On spawn: pulls an indicator from the pool, assigns it to the newest CuBot.
//   - On death: finds the indicator tracking that CuBot, releases it back to pool.
//   - A single coroutine drives the shared pulse interval for ALL active indicators —
//     this is cheaper than one coroutine per indicator and keeps timing centralized.
//   - UpdatePosition() is called on all active indicators in Update(), not LateUpdate,
//     because these are screen-space elements and don't depend on camera transform.
//
// IMPORTANT — OnCuBotSpawn limitation:
//   MB_CuBotBase.OnCuBotSpawn fires in OnEnable(), which means we receive the event
//   but we don't receive a reference to WHICH CuBot just spawned. To work around this,
//   we defer assignment by one frame using a coroutine so the CuBot's Transform is
//   fully initialized and we can scan active CuBots that don't yet have an indicator.
//
// Inspector setup:
//   - Place this component on the Screen Space — Overlay Canvas in your Stage scene.
//   - IndicatorPrefab: a prefab with RectTransform, Image, and Mb_CuBotIndicator
//   - InitialPoolSize: how many indicators to pre-warm (default 20)
//   - EdgeMargin: pixels from screen edge the indicator stops at (default 40)
//   - PulseInterval: seconds between automatic pulses (default 5)
//   - PulseScale: peak scale during pulse pop (default 1.3)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mb_CuBotIndicatorManager : MonoBehaviour
{
    #region Singleton           //----------------------------------------

    public static Mb_CuBotIndicatorManager Instance { get; private set; }

    #endregion                  //----------------------------------------


    #region Inspector Fields    //----------------------------------------

    [Header("Indicator Prefab")]
    [SerializeField] private Mb_CuBotIndicator IndicatorPrefab;

    [Header("Pool Settings")]
    // TODO: Tune InitialPoolSize — 20 should cover the max simultaneous enemies in one wave.
    [SerializeField] private int InitialPoolSize = 20;

    [Header("Layout")]
    [Tooltip("Distance from the screen edge where indicators are clamped, in pixels.")]
    [SerializeField] private float EdgeMargin = 40f;

    [Header("Pulse Animation")]
    [Tooltip("Seconds between automatic pulse pops while a CuBot is alive.")]
    [SerializeField] private float PulseInterval = 5f;

    [Tooltip("Peak scale multiplier during the pulse animation (1 = no pop).")]
    [SerializeField] private float PulseScale = 1.3f;

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    // Pool of inactive indicators ready to be assigned
    private Queue<Mb_CuBotIndicator> _indicatorPool = new Queue<Mb_CuBotIndicator>();

    // Maps each active CuBot's GameObject to its assigned indicator.
    // We key by GameObject because that's what OnCuBotDeath passes us.
    private Dictionary<GameObject, Mb_CuBotIndicator> _activeIndicators
        = new Dictionary<GameObject, Mb_CuBotIndicator>();

    // Cached camera — fetched once in Awake
    private Camera _mainCamera;

    // Handle on the shared pulse coroutine so we can stop it on disable
    private Coroutine _pulseCoroutine;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _mainCamera = Camera.main;

        if (_mainCamera == null)
            Debug.LogError("[Mb_CuBotIndicatorManager] No main camera found in scene.");

        if (IndicatorPrefab == null)
            Debug.LogError("[Mb_CuBotIndicatorManager] IndicatorPrefab is not assigned.");

        PrewarmPool();
    }


    private void OnEnable()
    {
        MB_CuBotBase.OnCuBotSpawn += HandleCuBotSpawn;
        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath;

        // Start the shared pulse loop — one coroutine drives pulses for every indicator
        _pulseCoroutine = StartCoroutine(SharedPulseLoop());
    }


    private void OnDisable()
    {
        MB_CuBotBase.OnCuBotSpawn -= HandleCuBotSpawn;
        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }
    }


    private void Update()
    {
        // Drive position updates for every active indicator each frame.
        // We iterate the dictionary values — this avoids a separate active list.
        foreach (var indicator in _activeIndicators.Values)
            indicator.UpdatePosition();
    }

    #endregion                  //----------------------------------------


    #region Event Handlers      //----------------------------------------

    private void HandleCuBotSpawn(GameObject obj)
    {
        // OnCuBotSpawn fires in MB_CuBotBase.OnEnable() but passes no reference.
        // We defer by one frame so the CuBot's Transform is fully repositioned
        // by WaveManager before we try to track it.
        StartCoroutine(AssignIndicatorNextFrame());
    }


    private void HandleCuBotDeath(GameObject deadCuBot)
    {
        if (!_activeIndicators.TryGetValue(deadCuBot, out Mb_CuBotIndicator indicator))
            return;

        // Release the indicator back to the pool
        indicator.Release();
        _indicatorPool.Enqueue(indicator);
        _activeIndicators.Remove(deadCuBot);

        Debug.Log($"[Mb_CuBotIndicatorManager] Indicator released for {deadCuBot.name}. " +
                  $"Pool size: {_indicatorPool.Count}");
    }

    #endregion                  //----------------------------------------


    #region Assignment          //----------------------------------------

    // Defers indicator assignment one frame so the spawned CuBot is
    // fully repositioned and active before we scan for it.
    private IEnumerator AssignIndicatorNextFrame()
    {
        yield return null;

        // Find the first active CuBot that doesn't have an indicator yet.
        // We look for MB_CuBotBase components in the scene rather than trusting
        // a stale reference, since OnCuBotSpawn passes no GameObject argument.
        MB_CuBotBase[] allCuBots = FindObjectsByType<MB_CuBotBase>(
            FindObjectsSortMode.None
        );

        foreach (MB_CuBotBase cuBot in allCuBots)
        {
            // Skip inactive CuBots (pool objects that are sitting idle)
            if (!cuBot.gameObject.activeInHierarchy) continue;

            // Skip CuBots already tracked
            if (_activeIndicators.ContainsKey(cuBot.gameObject)) continue;

            // This CuBot has no indicator — assign one
            Mb_CuBotIndicator indicator = GetOrCreateIndicator();
            indicator.Assign(cuBot.transform);

            // Trigger the initial spawn pulse
            indicator.TriggerPulse();

            _activeIndicators[cuBot.gameObject] = indicator;

            Debug.Log($"[Mb_CuBotIndicatorManager] Indicator assigned to {cuBot.gameObject.name}. " +
                      $"Active: {_activeIndicators.Count}");

            // Only assign one indicator per spawn event — the next spawn event
            // will fire its own coroutine and pick up the next untracked CuBot
            break;
        }
    }

    #endregion                  //----------------------------------------


    #region Pulse Loop          //----------------------------------------

    // A single coroutine that pulses every active indicator on a shared interval.
    // This is cheaper than one coroutine per indicator because there's only one
    // WaitForSecondsRealtime allocation in flight at a time.
    // Uses unscaled time so it survives Time.timeScale = 0 during pause.
    private IEnumerator SharedPulseLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(PulseInterval);

            foreach (var indicator in _activeIndicators.Values)
                indicator.TriggerPulse();
        }
    }

    #endregion                  //----------------------------------------


    #region Pool Management     //----------------------------------------

    // Creates InitialPoolSize inactive indicators and queues them up.
    // Pre-warming avoids allocation spikes when the first wave starts.
    private void PrewarmPool()
    {
        for (int i = 0; i < InitialPoolSize; i++)
        {
            Mb_CuBotIndicator indicator = CreateIndicator();
            indicator.gameObject.SetActive(false);
            _indicatorPool.Enqueue(indicator);
        }

        Debug.Log($"[Mb_CuBotIndicatorManager] Pool pre-warmed with {InitialPoolSize} indicators.");
    }


    // Returns a pooled indicator, or creates a new one if the pool is empty.
    // Creating beyond InitialPoolSize is intentional — it self-heals if the
    // pool was sized too small, at the cost of a small allocation.
    private Mb_CuBotIndicator GetOrCreateIndicator()
    {
        if (_indicatorPool.Count > 0)
            return _indicatorPool.Dequeue();

        // Pool exhausted — create a new indicator and log a warning so we know
        // to raise InitialPoolSize before the next playtest
        Debug.LogWarning("[Mb_CuBotIndicatorManager] Pool exhausted — creating new indicator. " +
                         "Consider raising InitialPoolSize.");

        return CreateIndicator();
    }


    // Instantiates one indicator prefab, parents it to this Canvas, and configures it.
    private Mb_CuBotIndicator CreateIndicator()
    {
        Mb_CuBotIndicator indicator = Instantiate(
            IndicatorPrefab,
            parent: transform  // parent to this Canvas so it renders in screen space
        );

        // Pass shared config so every indicator uses the same tuning values
        indicator.Configure(_mainCamera, EdgeMargin, PulseScale);

        return indicator;
    }

    #endregion                  //----------------------------------------
}