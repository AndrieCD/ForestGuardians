// Mb_WildlifeDialogBinder.cs
// Plays a species-specific dialog sequence the first time a wildlife entry
// is unlocked via the Almanac system.
//
// HOW IT WORKS:
//   - Subscribes to Mb_AlmanacManager.OnEntryUnlocked (static event).
//   - When an entry unlocks, reads its DiscoverySequence field.
//   - If a sequence is assigned, enqueues it with Mb_DialogManager.
//   - Repeat completions (OnRepeatCompleted) are intentionally ignored —
//     discovery narration only plays on first unlock.
//
// LIVES ON:
//   The Stage GameObject alongside Mb_StageDialogBinder,
//   Mb_WaveDialogBinder, and Mb_EnemyTypeDialogBinder.
//
// INSPECTOR SETUP:
//   No fields required. All data comes from SO_WildlifeEntry.DiscoverySequence.
//   Just add this component to the Stage GameObject and it works automatically.
//
// AUDIO NOTE:
//   Assign AudioClip on each SO_Dialog within the DiscoverySequence assets
//   once voice acting is ready. Until then, Mb_DialogManager's
//   FallbackDismissDuration handles timing automatically.

using UnityEngine;

public class Mb_WildlifeDialogBinder : MonoBehaviour
{
    private void OnEnable()
    {
        Mb_AlmanacManager.OnEntryUnlocked += HandleEntryUnlocked;
    }

    private void OnDisable()
    {
        Mb_AlmanacManager.OnEntryUnlocked -= HandleEntryUnlocked;
    }


    private void HandleEntryUnlocked(SO_WildlifeEntry entry)
    {
        if (entry == null) return;

        // No sequence assigned yet — silently skip
        // This is expected during early development before all sequences are created
        if (entry.DiscoverySequence == null) return;

        if (Mb_DialogManager.Instance == null)
        {
            Debug.LogWarning("[Mb_WildlifeDialogBinder] No Mb_DialogManager instance found " +
                             $"when trying to play discovery dialog for '{entry.CommonName}'.");
            return;
        }

        Mb_DialogManager.Instance.EnqueueSequence(entry.DiscoverySequence);

        Debug.Log($"[Mb_WildlifeDialogBinder] Enqueued discovery dialog for '{entry.CommonName}'.");
    }
}