// Mb_WildlifeSpawnPoint.cs
// A small MonoBehaviour placed on each pre-positioned spawn point Transform
// in the stage scene. Holds the habitat type for that location so
// Mb_WildlifeDiscoveryManager can filter valid points per species.
//
// Inspector setup:
//   - Place this component on empty GameObjects positioned in the stage
//   - Set HabitatType to match the physical environment at that location
//     (e.g. a point near a river = Aquatic, a point on a tree branch = Arboreal)
//   - Organize all spawn points as children of a single "WildlifeSpawnPoints"
//     GameObject in the scene hierarchy for easy management
//   - Mb_WildlifeDiscoveryManager finds these via its [SerializeField] list —
//     drag all spawn point GameObjects into that list in the Inspector

using UnityEngine;

public class Mb_WildlifeSpawnPoint : MonoBehaviour
{
    [Tooltip("The habitat type at this location. " +
             "A species can spawn here if its AcceptedHabitats flags include this type.")]
    public HabitatType HabitatType = HabitatType.Ground;

    // Whether this point is currently occupied by a spawned animal.
    // Set to true when an animal is spawned here, false when the animal
    // is collected — prevents two animals sharing the same point.
    [HideInInspector] public bool IsOccupied = false;

    // Runtime reference to whichever animal currently occupies this point.
    // Cleared when the animal is collected.
    [HideInInspector] public GameObject OccupyingAnimal = null;


    /// <summary>
    /// Returns true if this spawn point's habitat is compatible with
    /// the given species' accepted habitats.
    /// Uses bitwise AND — valid if ANY flag overlaps.
    /// </summary>
    public bool IsCompatibleWith(HabitatType acceptedHabitats)
    {
        return (HabitatType & acceptedHabitats) != 0;
    }


    /// <summary>
    /// Marks this point as occupied by the given animal GameObject.
    /// Called by Mb_WildlifeDiscoveryManager after spawning.
    /// </summary>
    public void Occupy(GameObject animal)
    {
        IsOccupied = true;
        OccupyingAnimal = animal;
    }


    /// <summary>
    /// Clears the occupied state. Called by Mb_WildlifeAnimal on collection.
    /// </summary>
    public void Vacate()
    {
        IsOccupied = false;
        OccupyingAnimal = null;
    }


    // Draws a colored gizmo in the Scene view so level designers can see
    // spawn point locations and habitat types without entering Play mode.
    private void OnDrawGizmos()
    {
        // Color-code by habitat type for easy identification in the editor
        Gizmos.color = HabitatType switch
        {
            HabitatType.Ground => new Color(0.6f, 0.4f, 0.1f, 0.8f),  // Brown
            HabitatType.Arboreal => new Color(0.1f, 0.7f, 0.1f, 0.8f),  // Green
            HabitatType.Aerial => new Color(0.4f, 0.7f, 1.0f, 0.8f),  // Sky blue
            HabitatType.Aquatic => new Color(0.1f, 0.3f, 0.9f, 0.8f),  // Deep blue
            _ => new Color(1.0f, 1.0f, 1.0f, 0.8f),  // White fallback
        };

        Gizmos.DrawSphere(transform.position, 0.4f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
    }
}