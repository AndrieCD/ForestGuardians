// Mb_AlmanacPanelSlot.cs
// Lives on each Panel child under DiamondGrid.
// Declares which SO_WildlifeEntry belongs to this diamond position.
//
// Mb_AlmanacUI reads this component from each Panel child at build time
// instead of iterating AllEntries in order — this way the visual layout
// of the diamond grid is controlled entirely in the Inspector, not in code.
//
// Inspector setup:
//   - Add this component to each of the 13 Panel GameObjects under DiamondGrid
//   - Assign the correct SO_WildlifeEntry to each Panel's Entry field
//   - Leave Entry null on any Panel you want to keep empty (no card spawned)

using UnityEngine;

public class Mb_AlmanacPanelSlot : MonoBehaviour
{
    [Tooltip("The wildlife entry that occupies this diamond panel position. " +
             "Drag the matching SO_WildlifeEntry asset here. " +
             "Leave null to keep this panel empty.")]
    public SO_WildlifeEntry Entry;
}