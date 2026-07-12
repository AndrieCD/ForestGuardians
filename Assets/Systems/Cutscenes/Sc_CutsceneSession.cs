using UnityEngine;

public static class Sc_CutsceneSession
{
    public static E_CutsceneId ActiveCutsceneId { get; private set; } = E_CutsceneId.None;
    public static E_CutsceneDestination ContinueDestination { get; private set; } = E_CutsceneDestination.None;
    public static int ContinueStageNumber { get; private set; } = 0;
    public static string ContinueSceneName { get; private set; } = string.Empty;

    public static E_CutsceneId PendingCutsceneId { get; private set; } = E_CutsceneId.None;
    public static int PendingStageNumber { get; private set; } = 0;

    public static bool HasActiveCutscene => ActiveCutsceneId != E_CutsceneId.None;
    public static bool HasPendingCutsceneBeforeStage => PendingCutsceneId != E_CutsceneId.None;

    public static void SetActiveCutscene(
        E_CutsceneId cutsceneId,
        E_CutsceneDestination continueDestination,
        int continueStageNumber = 0,
        string continueSceneName = "")
    {
        ActiveCutsceneId = cutsceneId;
        ContinueDestination = continueDestination;
        ContinueStageNumber = continueStageNumber;
        ContinueSceneName = continueSceneName ?? string.Empty;

        Debug.Log($"[Sc_CutsceneSession] Active cutscene set: {ActiveCutsceneId}, " +
                  $"Destination: {ContinueDestination}, Stage: {ContinueStageNumber}, " +
                  $"Scene: {ContinueSceneName}.");
    }

    public static void SetPendingStageCutscene(E_CutsceneId cutsceneId, int targetStageNumber)
    {
        PendingCutsceneId = cutsceneId;
        PendingStageNumber = targetStageNumber;

        Debug.Log($"[Sc_CutsceneSession] Pending cutscene set: {PendingCutsceneId}, " +
                  $"Target stage: {PendingStageNumber}.");
    }

    public static bool ConsumePendingStageCutscene()
    {
        if (!HasPendingCutsceneBeforeStage)
            return false;

        SetActiveCutscene(PendingCutsceneId, E_CutsceneDestination.Stage, PendingStageNumber);
        ClearPendingCutscene();
        return true;
    }

    public static void ClearActiveCutscene()
    {
        ActiveCutsceneId = E_CutsceneId.None;
        ContinueDestination = E_CutsceneDestination.None;
        ContinueStageNumber = 0;
        ContinueSceneName = string.Empty;

        Debug.Log("[Sc_CutsceneSession] Active cutscene cleared.");
    }

    public static void ClearPendingCutscene()
    {
        PendingCutsceneId = E_CutsceneId.None;
        PendingStageNumber = 0;

        Debug.Log("[Sc_CutsceneSession] Pending cutscene cleared.");
    }

    public static void ClearAll()
    {
        ClearActiveCutscene();
        ClearPendingCutscene();
    }
}
