// Mb_WildlifeAnimal.cs
// Lives on each wildlife animal prefab spawned in the stage.
// Handles gaze-based collection detection, VFX child management,
// and reporting collection to Mb_WildlifeDiscoveryManager.
//
// GAZE DETECTION:
//   Every frame, a raycast is fired from Camera.main's screen center.
//   If the ray hits this animal's collider on the Wildlife layer within
//   MAX_GAZE_DISTANCE, a timer increments. Any frame without a valid hit
//   resets the timer to zero (no pause-and-resume).
//   When the timer reaches Entry.GazeDuration, the animal is "collected."
//
// COLLECTION BEHAVIOR:
//   - The animal STAYS visible in the scene after collection (never deactivates)
//   - The child VFX GameObject is deactivated on collection
//   - The gaze detection is permanently disabled so re-collection is impossible
//   - The owning spawn point is vacated via Mb_WildlifeSpawnPoint.Vacate()
//
// POOL NOTE:
//   These animals are Instantiated (not pooled) per stage run and destroyed
//   when the stage scene unloads. No pool-safety logic is needed here.
//
// Inspector setup (on prefab):
//   - Assign a Collider component on this GameObject (any shape)
//   - Set the GameObject's layer to "Wildlife" in the Inspector
//   - Add a child GameObject named "VFX" with your particle system or
//     glow effect — this is what gets deactivated on collection
//   - Entry is set at runtime via Initialize() — do not assign in prefab Inspector
//   - GazeProgressBar is optional — assign a child UI fill image if you want
//     a visible progress indicator while the player gazes
//     // TODO: Wire GazeProgressBar once HUD art is ready

using UnityEngine;
using UnityEngine.UI;

public class Mb_WildlifeAnimal : MonoBehaviour
{

    #region Constants                   //----------------------------------------

    // Maximum raycast distance for gaze detection.
    // Animals beyond this range cannot be collected even if the crosshair is on them.
    private const float MAX_GAZE_DISTANCE = 100f;

    // Name of the child GameObject that holds the visibility VFX.
    // Must match the child name on every wildlife prefab exactly.
    // TODO: Confirm this naming convention with whoever builds the prefabs.
    private const string VFX_CHILD_NAME = "VFX";

    private bool _isQuestSpecies = false;

    #endregion                          //----------------------------------------


    #region Inspector Fields            //----------------------------------------

    [Header("Gaze Progress (Optional)")]
    [Tooltip("Optional UI Image (fill type) that shows gaze progress as a radial fill. " +
             "Leave unassigned if no progress indicator is needed yet.")]
    // TODO: Assign once HUD art is ready — works without it
    [SerializeField] private Image GazeProgressBar;

    #endregion                          //----------------------------------------


    #region Runtime State               //----------------------------------------

    // Set by Mb_WildlifeDiscoveryManager.SpawnAllWildlife() after instantiation.
    // Never assign this on the prefab — it is always injected at runtime.
    public SO_WildlifeEntry Entry { get; private set; }

    // Reference back to the manager so we can call ReportCollection()
    private Mb_WildlifeDiscoveryManager _discoveryManager;

    // The spawn point this animal occupies — stored so we can vacate it on collection
    private Mb_WildlifeSpawnPoint _spawnPoint;

    // How long the player has continuously gazed at this animal this attempt
    private float _gazeTimer = 0f;

    // True once this animal has been collected — disables all further gaze detection
    private bool _isCollected = false;

    // Cached child VFX GameObject — found once in Initialize(), toggled on collection
    private GameObject _vfxChild;

    // Cached collider — used to confirm the raycast hit this specific animal
    private Collider _collider;

    // Layer mask for the Wildlife layer — built once in Initialize()
    private int _wildlifeLayerMask;

    #endregion                          //----------------------------------------


    #region Initialization              //----------------------------------------

    /// <summary>
    /// Called by Mb_WildlifeDiscoveryManager immediately after Instantiate().
    /// Sets up all runtime references so the prefab needs no pre-assigned fields.
    /// </summary>
    public void Initialize(
        SO_WildlifeEntry entry,
        Mb_WildlifeDiscoveryManager manager,
        Mb_WildlifeSpawnPoint spawnPoint = null,
        bool isQuestSpecies = false)
    {
        Entry = entry;
        _discoveryManager = manager;
        _spawnPoint = spawnPoint;

        // Cache the collider on this GameObject — used to confirm raycast hits
        _collider = GetComponent<Collider>();
        if (_collider == null)
            Debug.LogError($"[Mb_WildlifeAnimal] '{entry.CommonName}' prefab has no Collider. " +
                           $"Gaze detection will not work.");

        // Find the VFX child by name — warn if missing but don't crash
        Transform vfxTransform = transform.Find(VFX_CHILD_NAME);
        if (vfxTransform != null)
            _vfxChild = vfxTransform.gameObject;
        else
            Debug.LogWarning($"[Mb_WildlifeAnimal] '{entry.CommonName}' prefab has no child " +
                             $"named '{VFX_CHILD_NAME}'. VFX will not play on collection.");

        // Build the layer mask once — only raycasts against the Wildlife layer
        _wildlifeLayerMask = 1 << LayerMask.NameToLayer("Wildlife");

        // Ensure VFX starts active so the animal is visible on spawn
        _vfxChild?.SetActive(true);

        if (_vfxChild != null)
            _vfxChild.SetActive(isQuestSpecies);

        // Reset gaze state in case the component is reused
        _gazeTimer = 0f;
        _isCollected = false;

        _isQuestSpecies = isQuestSpecies;
    }

    #endregion                          //----------------------------------------


    #region Unity Lifecycle             //----------------------------------------

    private void Update()
    {
        // Never process gaze after collection or before initialization
        if (_isCollected) return;
        if (Entry == null) return;
        if (Camera.main == null) return;

        UpdateGaze();
    }

    #endregion                          //----------------------------------------


    #region Gaze Detection              //----------------------------------------

    // REPLACE the existing UpdateGaze() method with this:

    private void UpdateGaze()
    {
        // Fire a ray from the camera's screen center (crosshair position)
        Ray gazeRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        bool isBeingGazedAt = false;

        // Only register hits on the Wildlife layer within max distance
        if (Physics.Raycast(gazeRay, out RaycastHit hit,
                            MAX_GAZE_DISTANCE, _wildlifeLayerMask))
        {
            // Confirm the hit is specifically THIS animal's collider —
            // not another wildlife animal that happens to be in line
            if (hit.collider == _collider)
                isBeingGazedAt = true;
        }

        if (!_isQuestSpecies) return;
        if (isBeingGazedAt)
        {
            // Accumulate gaze time — unscaled so pause doesn't affect it
            // TODO: Switch to Time.deltaTime if pausing should halt gaze progress
            _gazeTimer += Time.deltaTime;

            // Report progress to the shared ring UI — it shows itself automatically
            Mb_GazeProgressUI.Instance?.ReportGaze(_gazeTimer / Entry.GazeDuration);

            // Check if the player has held gaze long enough
            if (_gazeTimer >= Entry.GazeDuration)
                Collect();
        }
        else
        {
            // Gaze broken — reset fully (no pause-and-resume)
            if (_gazeTimer > 0f)
            {
                _gazeTimer = 0f;
                Mb_GazeProgressUI.Instance?.HideRing();
            }
        }
    }

    #endregion                          //----------------------------------------


    #region Collection                  //----------------------------------------

    private void Collect()
    {
        // Guard against double-collection from a single long gaze frame
        if (_isCollected) return;

        _isCollected = true;

        // Deactivate VFX — animal stays visible but no longer glows/pulses
        _vfxChild?.SetActive(false);

        Mb_GazeProgressUI.Instance?.HideRing();


        // Vacate the spawn point so it's marked available for future use
        // (relevant if a system ever wants to respawn animals — not currently planned)
        _spawnPoint?.Vacate();

        // Notify the discovery manager — handles quest progress and almanac unlock
        _discoveryManager?.ReportCollection(Entry, _spawnPoint);

        // TODO: Play collection VFX here
        // Suggested: Mb_VFXManager.Instance.PlayAtPosition("WildlifeCollect",
        //                transform.position)
        // Or activate a second child VFX named "CollectVFX" for a burst effect
        Mb_VFXManager.Play(VFXType.Almanac_Collected, transform.position);


        // TODO: Play collection SFX here
        // Suggested: Mb_AudioManager.Instance.PlaySFX("WildlifeCollect")
        // Or use a species-specific sound keyed by entry.CommonName
        Mb_AudioManager.PlayEnvironmentSFX(EnvironmentSFX.Almanac_Collected, transform.position);

        Debug.Log($"[Mb_WildlifeAnimal] '{Entry.CommonName}' collected " +
                  $"at {transform.position}.");
    }

    #endregion                          //----------------------------------------


    #region Debug                       //----------------------------------------

    // Draws a gaze progress arc in the Scene view while the animal is being gazed at.
    // Only visible in the Editor — zero runtime cost in builds.
    private void OnDrawGizmosSelected()
    {
        if (Entry == null || _isCollected) return;

        // Draw a wire sphere showing the animal's approximate collection presence
        Gizmos.color = _gazeTimer > 0f
            ? Color.Lerp(Color.white, Color.green, _gazeTimer / Entry.GazeDuration)
            : Color.white;

        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }

    #endregion                          //----------------------------------------
}