// Mb_WildlifeDiscoveryManager.cs
// Lives on the Stage GameObject alongside Mb_StageManager and Mb_WaveManager.
// Manages the in-stage wildlife discovery quest for one stage run.
//
// RESPONSIBILITIES:
//   - On stage start: select quest species and background species
//   - Spawn all individuals across compatible, unoccupied spawn points
//   - Track per-species found counts against required counts
//   - Notify Mb_AlmanacManager when a species is fully completed
//   - Fire progress and completion events for the hotbar UI
//
// SPAWN FILL RULES:
//   1. Calculate total slots = ceil(spawnPointCount * FillFraction)
//   2. Guarantee quest species fill first (RequiredCount + bonus margin each)
//   3. Fill remaining slots with background species from this stage,
//      weighted by conservation status spawn weight
//   4. Each individual is placed at a randomly selected compatible,
//      unoccupied spawn point
//
// Inspector setup:
//   - Set CurrentStageNumber to match the stage scene (1, 2, or 3)
//   - Adjust FillFraction (default 0.5) for density tuning
//   - Adjust QuestSlotCount (default 3) for how many species are quested per run
//   - Assign all Mb_WildlifeSpawnPoint components via AllSpawnPoints list
//   - CriticallyEndangeredWeight, EndangeredWeight, NearThreatenedWeight
//     control background species spawn frequency

using System.Collections.Generic;
using UnityEngine;

public class Mb_WildlifeDiscoveryManager : MonoBehaviour
{

    public static Mb_WildlifeDiscoveryManager instance { get; private set; }


    #region Inspector Fields            //----------------------------------------

    [Header("Stage Settings")]
    [Tooltip("Which stage this scene represents. Used to filter species by ActiveStages.")]
    // TODO: Set this per scene — Stage 1 = 1, Stage 2 = 2, Stage 3 = 3
    [SerializeField] private int CurrentStageNumber = 1;

    [Tooltip("Fraction of all spawn points to fill with animals. " +
             "0.5 = half, 1.0 = all. Rounded up to the nearest whole number.")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float FillFraction = 0.5f;

    [Tooltip("How many species are assigned as quest targets per run.")]
    [SerializeField] private int QuestSlotCount = 3;

    [Header("Spawn Points")]
    [Tooltip("All Mb_WildlifeSpawnPoint components in this stage scene. " +
             "Drag all spawn point GameObjects here.")]
    [SerializeField]
    private List<Mb_WildlifeSpawnPoint> AllSpawnPoints
        = new List<Mb_WildlifeSpawnPoint>();

    [Header("Conservation Status Spawn Weights")]
    [Tooltip("Relative spawn weight for Critically Endangered background species. " +
             "Lower = rarer. Suggested default: 0.25")]
    [SerializeField] private float CriticallyEndangeredWeight = 0.25f;

    [Tooltip("Relative spawn weight for Endangered background species. " +
             "Suggested default: 0.50")]
    [SerializeField] private float EndangeredWeight = 0.50f;

    [Tooltip("Relative spawn weight for Near Threatened background species. " +
             "Suggested default: 1.0")]
    [SerializeField] private float NearThreatenedWeight = 1.0f;

    [Tooltip("How many extra individuals to spawn per quest species beyond " +
             "RequiredCount. Gives the player margin for error. Range: 1–2")]
    [SerializeField] private int QuestSpawnMargin = 1;

    #endregion                          //----------------------------------------


    #region Events                      //----------------------------------------

    // Fired whenever a quest species' found count changes.
    // Mb_WildlifeHotbarUI subscribes to update the slot display.
    // (entry, foundCount, requiredCount)
    public static event System.Action<SO_WildlifeEntry, int, int> OnSpeciesProgress;

    // Fired when a quest species reaches its RequiredCount.
    // Mb_AlmanacManager.UnlockEntry() or RecordRepeatCompletion() is called here.
    public static event System.Action<SO_WildlifeEntry> OnSpeciesCompleted;

    // Fired when ALL quest species for this run are completed.
    // Stub — wire to SFX/VFX/UI celebration here.
    public static event System.Action OnAllQuestsCompleted;

    #endregion                          //----------------------------------------


    #region Runtime State               //----------------------------------------

    // The 3 species assigned as quest targets this run
    private List<SO_WildlifeEntry> _questSpecies = new List<SO_WildlifeEntry>();

    // Tracks how many individuals of each quest species have been found
    // Key: SO_WildlifeEntry reference, Value: found count
    private Dictionary<SO_WildlifeEntry, int> _foundCounts
        = new Dictionary<SO_WildlifeEntry, int>();

    // Tracks which quest species are fully completed this run
    private HashSet<SO_WildlifeEntry> _completedSpecies
        = new HashSet<SO_WildlifeEntry>();

    #endregion                          //----------------------------------------


    #region Unity Lifecycle             //----------------------------------------

    private void OnEnable()
    {
        Mb_StageManager.OnStageStart += HandleStageStart;
        Mb_StageManager.OnStageEnd += HandleStageEnd;
    }

    private void OnDisable()
    {
        Mb_StageManager.OnStageStart -= HandleStageStart;
        Mb_StageManager.OnStageEnd -= HandleStageEnd;

        if (instance == this) instance = null;

    }

    private void Awake()
    {
        instance = this;
    }

    #endregion                          //----------------------------------------


    #region Stage Lifecycle             //----------------------------------------

    private void HandleStageStart()
    {
        SelectQuestSpecies();
        SpawnAllWildlife();

        Debug.Log($"[Mb_WildlifeDiscoveryManager] Stage {CurrentStageNumber} started. " +
                  $"Quest species: {_questSpecies.Count}");
    }

    private void HandleStageEnd()
    {
        // Clear runtime state — this manager lives on the stage GameObject
        // which is destroyed on scene unload, so this is mostly defensive
        _questSpecies.Clear();
        _foundCounts.Clear();
        _completedSpecies.Clear();
    }

    #endregion                          //----------------------------------------


    #region Quest Species Selection     //----------------------------------------

    private void SelectQuestSpecies()
    {
        _questSpecies.Clear();
        _foundCounts.Clear();
        _completedSpecies.Clear();

        if (Mb_AlmanacManager.Instance == null)
        {
            Debug.LogError("[Mb_WildlifeDiscoveryManager] Mb_AlmanacManager not found. " +
                           "Cannot select quest species.");
            return;
        }

        // Get all entries valid for this stage
        List<SO_WildlifeEntry> stageEntries = GetStageEntries();

        // Priority 1: locked entries for this stage
        List<SO_WildlifeEntry> lockedEntries = new List<SO_WildlifeEntry>();
        foreach (SO_WildlifeEntry entry in stageEntries)
            if (!Mb_AlmanacManager.Instance.IsUnlocked(entry))
                lockedEntries.Add(entry);

        // Shuffle locked entries for random selection
        Shuffle(lockedEntries);

        // Fill quest slots with locked entries first
        foreach (SO_WildlifeEntry entry in lockedEntries)
        {
            if (_questSpecies.Count >= QuestSlotCount) break;
            _questSpecies.Add(entry);
        }

        // Priority 2: if not enough locked entries, fill with unlocked stage entries
        // weighted by rarity (Critically Endangered least likely to repeat)
        if (_questSpecies.Count < QuestSlotCount)
        {
            List<SO_WildlifeEntry> unlockedEntries = new List<SO_WildlifeEntry>();
            foreach (SO_WildlifeEntry entry in stageEntries)
                if (Mb_AlmanacManager.Instance.IsUnlocked(entry)
                    && !_questSpecies.Contains(entry))
                    unlockedEntries.Add(entry);

            List<SO_WildlifeEntry> weightedUnlocked
                = BuildWeightedList(unlockedEntries);

            Shuffle(weightedUnlocked);

            // Track which entries we've already added to avoid duplicates
            HashSet<SO_WildlifeEntry> alreadyAdded
                = new HashSet<SO_WildlifeEntry>(_questSpecies);

            foreach (SO_WildlifeEntry entry in weightedUnlocked)
            {
                if (_questSpecies.Count >= QuestSlotCount) break;
                if (alreadyAdded.Contains(entry)) continue;

                _questSpecies.Add(entry);
                alreadyAdded.Add(entry);
            }
        }

        // Initialize found counts for all quest species
        foreach (SO_WildlifeEntry entry in _questSpecies)
            _foundCounts[entry] = 0;

        Debug.Log("[Mb_WildlifeDiscoveryManager] Quest species selected: " +
                  string.Join(", ", _questSpecies.ConvertAll(e => e.CommonName)));
    }

    #endregion                          //----------------------------------------


    #region Spawning                    //----------------------------------------

    private void SpawnAllWildlife()
    {
        // Calculate how many total spawn slots to fill — rounded up
        int totalSlots = Mathf.CeilToInt(AllSpawnPoints.Count * FillFraction);

        // Reset all spawn points to unoccupied
        foreach (Mb_WildlifeSpawnPoint point in AllSpawnPoints)
            point.Vacate();

        // --- Step 1: Guarantee quest species spawn first ---
        List<SO_WildlifeEntry> spawnQueue = new List<SO_WildlifeEntry>();

        foreach (SO_WildlifeEntry entry in _questSpecies)
        {
            int spawnCount = entry.RequiredCount + QuestSpawnMargin;
            for (int i = 0; i < spawnCount; i++)
                spawnQueue.Add(entry);
        }

        // --- Step 2: Fill remaining slots with background species ---
        int remainingSlots = totalSlots - spawnQueue.Count;

        if (remainingSlots > 0)
        {
            // Background pool: stage-valid species not already in quest,
            // weighted by conservation status
            List<SO_WildlifeEntry> stageEntries = GetStageEntries();
            List<SO_WildlifeEntry> backgroundPool = new List<SO_WildlifeEntry>();

            foreach (SO_WildlifeEntry entry in stageEntries)
                if (!_questSpecies.Contains(entry))
                    backgroundPool.Add(entry);

            List<SO_WildlifeEntry> weightedBackground
                = BuildWeightedList(backgroundPool);

            Shuffle(weightedBackground);

            // Fill remaining slots — allow the same species to appear
            // multiple times as background (natural feel)
            int bgIndex = 0;
            for (int i = 0; i < remainingSlots && weightedBackground.Count > 0; i++)
            {
                spawnQueue.Add(weightedBackground[bgIndex % weightedBackground.Count]);
                bgIndex++;
            }
        }

        // --- Step 3: Spawn each entry in the queue at a compatible point ---
        Shuffle(spawnQueue);

        foreach (SO_WildlifeEntry entry in spawnQueue)
        {
            if (entry.SpawnPrefab == null)
            {
                Debug.LogWarning($"[Mb_WildlifeDiscoveryManager] '{entry.CommonName}' " +
                                 $"has no SpawnPrefab assigned — skipping.");
                continue;
            }

            Mb_WildlifeSpawnPoint point = GetCompatiblePoint(entry.AcceptedHabitats);

            if (point == null)
            {
                Debug.LogWarning($"[Mb_WildlifeDiscoveryManager] No compatible unoccupied " +
                                 $"spawn point found for '{entry.CommonName}' " +
                                 $"(Habitat: {entry.AcceptedHabitats}). Skipping.");
                continue;
            }

            // Instantiate the animal at the spawn point
            GameObject animal = Instantiate(
                entry.SpawnPrefab,
                point.transform.position,
                point.transform.rotation
            );

            // Wire the entry reference into the animal component
            Mb_WildlifeAnimal animalComponent = animal.GetComponent<Mb_WildlifeAnimal>();
            if (animalComponent != null)
            {
                bool isQuest = _questSpecies.Contains(entry);
                animalComponent.Initialize(entry, this, point, isQuest);
            }
            else
                Debug.LogWarning($"[Mb_WildlifeDiscoveryManager] Spawned '{entry.CommonName}' " +
                                 $"prefab has no Mb_WildlifeAnimal component.");

            point.Occupy(animal);
        }

        Debug.Log($"[Mb_WildlifeDiscoveryManager] Spawned {spawnQueue.Count} animals " +
                  $"across {AllSpawnPoints.Count} spawn points " +
                  $"(fill fraction: {FillFraction}).");
    }


    /// <summary>
    /// Returns a random unoccupied spawn point compatible with the given habitat flags.
    /// Returns null if no compatible unoccupied point exists.
    /// </summary>
    private Mb_WildlifeSpawnPoint GetCompatiblePoint(HabitatType acceptedHabitats)
    {
        // Build a list of all valid candidates first, then pick randomly —
        // avoids bias toward earlier points in the list
        List<Mb_WildlifeSpawnPoint> candidates = new List<Mb_WildlifeSpawnPoint>();

        foreach (Mb_WildlifeSpawnPoint point in AllSpawnPoints)
            if (!point.IsOccupied && point.IsCompatibleWith(acceptedHabitats))
                candidates.Add(point);

        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    #endregion                          //----------------------------------------


    #region Collection Handling         //----------------------------------------

    /// <summary>
    /// Called by Mb_WildlifeAnimal when a quest species individual is collected.
    /// Non-quest species call this too — it silently ignores them after the
    /// spawn point vacate, so Mb_WildlifeAnimal doesn't need to know if it's
    /// a quest species or not.
    /// </summary>
    public void ReportCollection(SO_WildlifeEntry entry, Mb_WildlifeSpawnPoint spawnPoint)
    {
        // Vacate the spawn point regardless of quest status
        spawnPoint?.Vacate();

        // Only track progress for quest species
        if (!_foundCounts.ContainsKey(entry)) return;

        // Don't count beyond RequiredCount — extra individuals can still be
        // "found" visually but don't change quest state
        if (_completedSpecies.Contains(entry)) return;

        _foundCounts[entry]++;
        int found = _foundCounts[entry];
        int required = entry.RequiredCount;

        OnSpeciesProgress?.Invoke(entry, found, required);

        Debug.Log($"[Mb_WildlifeDiscoveryManager] '{entry.CommonName}' " +
                  $"{found}/{required} found.");





        if (found >= required)
            CompleteSpecies(entry);
    }


    private void CompleteSpecies(SO_WildlifeEntry entry)
    {
        _completedSpecies.Add(entry);

        // Route to the correct Almanac method based on unlock state
        if (Mb_AlmanacManager.Instance != null)
        {
            if (Mb_AlmanacManager.Instance.IsUnlocked(entry))
                Mb_AlmanacManager.Instance.RecordRepeatCompletion(entry);
            else
                Mb_AlmanacManager.Instance.UnlockEntry(entry);
        }

        OnSpeciesCompleted?.Invoke(entry);

        // TODO: Play quest species completion VFX here
        // Suggested: Mb_VFXManager.Instance.PlayAtPosition("QuestComplete",
        //                entry's last collected animal position)

        // TODO: Play quest species completion SFX here
        // Suggested: Mb_AudioManager.Instance.PlaySFX("QuestComplete")

        Debug.Log($"[Mb_WildlifeDiscoveryManager] Quest species '{entry.CommonName}' completed!");

        // Check if ALL quest species are done
        if (_completedSpecies.Count >= _questSpecies.Count)
        {
            OnAllQuestsCompleted?.Invoke();

            // TODO: Play all-quests-completed VFX here
            // TODO: Play all-quests-completed SFX here

            Debug.Log("[Mb_WildlifeDiscoveryManager] All quest species completed!");
        }
    }

    #endregion                          //----------------------------------------


    #region Public Read API             //----------------------------------------

    /// <summary>
    /// Returns the quest species list for this run.
    /// Used by Mb_WildlifeHotbarUI to initialize its slots on stage start.
    /// </summary>
    public IReadOnlyList<SO_WildlifeEntry> GetQuestSpecies()
    {
        return _questSpecies.AsReadOnly();
    }


    /// <summary>
    /// Returns the current found count for a quest species.
    /// Returns 0 for non-quest species.
    /// </summary>
    public int GetFoundCount(SO_WildlifeEntry entry)
    {
        return _foundCounts.TryGetValue(entry, out int count) ? count : 0;
    }

    #endregion                          //----------------------------------------


    #region Helpers                     //----------------------------------------

    /// <summary>
    /// Returns all SO_WildlifeEntry assets that are active in the current stage.
    /// </summary>
    private List<SO_WildlifeEntry> GetStageEntries()
    {
        List<SO_WildlifeEntry> result = new List<SO_WildlifeEntry>();

        foreach (SO_WildlifeEntry entry in
                 Mb_AlmanacManager.Instance.GetAllEntries())
            if (entry.ActiveStages.Contains(CurrentStageNumber))
                result.Add(entry);

        return result;
    }


    /// <summary>
    /// Builds a list where each entry appears N times proportional to its
    /// conservation status spawn weight. Weighted random selection is then
    /// achieved by shuffling and picking from this expanded list.
    ///
    /// CriticallyEndangered: weight 0.25 → appears 1 time (minimum)
    /// Endangered:           weight 0.50 → appears 2 times
    /// NearThreatened:       weight 1.00 → appears 4 times
    ///
    /// The multiplier of 4 is chosen so the minimum weight (0.25 * 4 = 1)
    /// gives at least one slot, keeping all species selectable.
    /// </summary>
    private List<SO_WildlifeEntry> BuildWeightedList(List<SO_WildlifeEntry> entries)
    {
        List<SO_WildlifeEntry> weighted = new List<SO_WildlifeEntry>();
        const int WEIGHT_MULTIPLIER = 4;

        foreach (SO_WildlifeEntry entry in entries)
        {
            float weight = entry.Status switch
            {
                ConservationStatus.CriticallyEndangered => CriticallyEndangeredWeight,
                ConservationStatus.Endangered => EndangeredWeight,
                ConservationStatus.NearThreatened => NearThreatenedWeight,
                _ => 1f
            };

            // Convert float weight to a slot count — minimum 1 so every
            // species always has at least one chance of being selected
            int slots = Mathf.Max(1, Mathf.RoundToInt(weight * WEIGHT_MULTIPLIER));
            for (int i = 0; i < slots; i++)
                weighted.Add(entry);
        }

        return weighted;
    }


    /// <summary>
    /// Fisher-Yates shuffle — unbiased in-place list randomization.
    /// Used for quest selection, background fill, and spawn ordering.
    /// </summary>
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion                          //----------------------------------------
}