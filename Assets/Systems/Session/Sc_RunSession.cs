// Sc_RunSession.cs
// Static class that holds transient per-run data — data that only needs to
// exist from guardian selection until the stage ends.
//
// WHY STATIC AND NOT A MONOBEHAVIOUR:
//   This data has no Unity lifecycle — it doesn't need Awake, Update, or
//   DontDestroyOnLoad. It is set in exactly one place (Mb_GuardianSelectionUI),
//   read in exactly one place (the stage scene's spawn logic), and cleared
//   in exactly one place (Mb_StageUnlockManager.HandleStageEnd).
//   A static class is the simplest correct tool for this job.
//
// WHY NOT SAVED TO DISK:
//   Guardian selection is a per-run choice — it has no meaning between sessions.
//   If the game crashes mid-run, the player simply selects again on next launch.
//   Persisting it would add complexity with no player benefit.
//
// USAGE:
//   // Set before loading the stage scene (in Mb_GuardianSelectionUI):
//   Sc_RunSession.SelectedGuardian = myGuardianSO;
//   Sc_RunSession.SelectedStageNumber = 1;
//
//   // Read in the stage scene (guardian spawner, stage manager, etc.):
//   SO_Guardian guardian = Sc_RunSession.SelectedGuardian;
//   int stage = Sc_RunSession.SelectedStageNumber;
//
//   // Clear after the stage ends (in Mb_StageUnlockManager.HandleStageEnd):
//   Sc_RunSession.Clear();

using UnityEngine;

public static class Sc_RunSession
{
    public const int STAGE_1 = 1;
    public const int STAGE_2 = 2;
    public const int STAGE_3 = 3;
    public const int TUTORIAL_STAGE = 4;

    public const int MIN_SELECTABLE_STAGE = STAGE_1;
    public const int MAX_SELECTABLE_STAGE = TUTORIAL_STAGE;

    // -------------------------------------------------------------------------
    // Session Data
    // -------------------------------------------------------------------------

    /// <summary>
    /// The SO_Guardian asset the player selected on the Guardian Selection screen.
    /// Null until the player confirms their selection.
    /// Read by the stage scene to determine which guardian prefab to activate.
    /// </summary>
    public static SO_Guardian SelectedGuardian { get; set; }

    /// <summary>
    /// The stage number (1, 2, or 3) the player selected on the Stage Selection screen.
    /// 0 until the player confirms their selection.
    /// Read by Mb_StageUnlockManager.HandleStageEnd to determine which stage
    /// was just completed and which stage to unlock next.
    /// </summary>
    public static int SelectedStageNumber { get; set; }


    // -------------------------------------------------------------------------
    // Session Control
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resets all session data to null/default.
    /// Called by Mb_StageUnlockManager after a stage ends and the unlock
    /// has been saved — ensures stale data never bleeds into the next run.
    /// </summary>
    public static void Clear()
    {
        SelectedGuardian = null;
        SelectedStageNumber = 0;

        Debug.Log("[Sc_RunSession] Session data cleared.");
    }


    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if both SelectedGuardian and SelectedStageNumber are set.
    /// Call this before loading a stage scene to catch missing selections early.
    /// </summary>
    public static bool IsValid()
    {
        bool guardianSet = SelectedGuardian != null;
        bool stageSet = SelectedStageNumber >= MIN_SELECTABLE_STAGE &&
                        SelectedStageNumber <= MAX_SELECTABLE_STAGE;

        if (!guardianSet)
            Debug.LogWarning("[Sc_RunSession] IsValid check failed — SelectedGuardian is null.");

        if (!stageSet)
            Debug.LogWarning($"[Sc_RunSession] IsValid check failed — " +
                             $"SelectedStageNumber is {SelectedStageNumber} " +
                             $"(must be {MIN_SELECTABLE_STAGE}–{MAX_SELECTABLE_STAGE}).");

        return guardianSet && stageSet;
    }
}
