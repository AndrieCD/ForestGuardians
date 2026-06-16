// SO_WildlifeEntry.cs
// ScriptableObject data container for one wildlife species in the Almanac.
//
// Every field here is read-only data — no runtime state lives here.
// Mb_AlmanacManager tracks unlock state in save data, not in this SO.
//
// Create in Project window:
//   Right-click > Create > ForestGuardians > WildlifeEntry
//
// Inspector setup:
//   - Fill all identity fields (CommonName, ScientificName, Status, Description)
//   - Assign SilhouetteIcon (shown while locked) and UnlockedIcon (shown when unlocked)
//   - Assign StatBonus — one Sc_StatEffect defining what stat and how much is granted
//   - Set RequiredCount (how many individuals the player must gaze at to unlock)
//   - Set GazeDuration (how many seconds of continuous gaze = one individual collected)
//   - Set ActiveStages (which stage numbers this species can appear in)
//   - Assign SpawnPrefab — the in-stage animal GameObject (must be on the Wildlife layer)
//   - ModelPrefab is optional until 3D models are ready

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New WildlifeEntry", menuName = "ForestGuardians/WildlifeEntry")]
public class SO_WildlifeEntry : ScriptableObject
{
    [Header("Identity")]
    public string CommonName;
    public string ScientificName;
    public ConservationStatus Status;
    [TextArea] public string Description;

    [Header("Icons")]
    [Tooltip("Shown in the Almanac UI while this entry is still locked — silhouette only.")]
    public Sprite SilhouetteIcon;

    [Tooltip("Shown in the Almanac UI after this entry has been unlocked.")]
    public Sprite UnlockedIcon;

    [Header("3D Model")]
    [Tooltip("Low-poly model displayed in the Almanac compendium viewer panel. " +
             "Leave unassigned until 3D models are ready.")]
    // TODO: Assign once low-poly 3D models are created for each species
    public GameObject ModelPrefab;

    [Header("Stat Bonus")]
    [Tooltip("The single stat effect granted to the Guardian when this entry is first unlocked. " +
             "Uses ModifierSource.Almanac so it can be cleanly reapplied on scene load.")]
    public Sc_StatEffect StatBonus;

    [Header("Collection Settings")]
    [Tooltip("How many individuals the player must gaze at to fully unlock this entry. " +
             "Recommended: 1 for rare species (Critically Endangered), up to 3 for common ones.")]
    // TODO: Tune per species — suggested defaults: CE = 1, E = 2, NT = 3
    public int RequiredCount = 1;

    [Tooltip("How many seconds of continuous camera gaze on one individual counts as a collection. " +
             "Birds on elevation may want a shorter window (1.5s); ground animals can be longer (2.5s).")]
    // TODO: Tune per species — suggested default: 2.0 seconds
    public float GazeDuration = 2.0f;

    [Header("Stage Availability")]
    [Tooltip("Which stage numbers (1, 2, or 3) this species can appear in during a run. " +
             "Mb_WildlifeDiscoveryManager filters the full entry list against the current stage number.")]
    public List<int> ActiveStages = new List<int>();

    [Header("In-Stage Spawning")]
    [Tooltip("The prefab spawned in the stage environment for this species. " +
             "Must have a Collider and be set to the 'Wildlife' layer.")]
    public GameObject SpawnPrefab;

    [Tooltip("Which habitat spawn points this species is compatible with. " +
         "A spawn point is valid if its HabitatType matches ANY of these flags. " +
         "e.g. Tamaraw accepts Ground | Aquatic.")]
    public HabitatType AcceptedHabitats = HabitatType.Ground;

    [Tooltip("Real-world population status description. " +
             "e.g. 'Fewer than 800 individuals remain in the wild.'")]
    public string Population;

    [Tooltip("Real-world habitat description. " +
             "e.g. 'Dense montane forests and river valleys of Mindanao.'")]
    public string Habitat;

    [Header("Discovery Dialog")]
    [Tooltip("Dialog sequence played when this species is unlocked for the first time. " +
         "Leave null for species that have no narration yet.")]
    public SO_DialogSequence DiscoverySequence;
}