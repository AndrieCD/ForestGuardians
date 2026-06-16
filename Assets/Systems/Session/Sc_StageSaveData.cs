// Sc_StageSaveData.cs
// Serializable save data class for stage unlock progression.
// Tracks which stages the player has unlocked across sessions.
//
// MIRRORS Sc_AlmanacSaveData — same JSON pattern, same file location convention.
// Stage 1 is always unlocked — Load() guarantees this on any fresh or missing file.
//
// SAVE FILE LOCATION:
//   Application.persistentDataPath/stage_save.json
//   On Windows: %AppData%/../LocalLow/<Company>/<Product>/stage_save.json
//
// USAGE:
//   // Load (call once on startup from Mb_StageUnlockManager):
//   Sc_StageSaveData save = Sc_StageSaveData.Load();
//
//   // Check if unlocked:
//   bool unlocked = save.Stage2Unlocked;
//
//   // Unlock and save:
//   save.Stage2Unlocked = true;
//   Sc_StageSaveData.Save(save);

using System;
using System.IO;
using UnityEngine;

[Serializable]
public class Sc_StageSaveData
{
    // -------------------------------------------------------------------------
    // Save File Path
    // -------------------------------------------------------------------------

    // TODO: Confirm this filename with the team before shipping.
    // Changing it after players have saves will invalidate their progress.
    private const string SAVE_FILE_NAME = "stage_save.json";

    private static string SaveFilePath =>
        Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);


    // -------------------------------------------------------------------------
    // Serialized Data
    // -------------------------------------------------------------------------

    // Stage 1 is always true — enforced in Load() so it can never be false
    // on a fresh install or a corrupted file.
    public bool Stage1Unlocked = true;

    // Set to true by Mb_StageUnlockManager when Stage 1 is completed.
    public bool Stage2Unlocked = false;

    // Set to true by Mb_StageUnlockManager when Stage 2 is completed.
    public bool Stage3Unlocked = false;

    // Tutorial is always true
    public bool TutorialUnlocked = true;

    // -------------------------------------------------------------------------
    // Convenience API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns whether the given stage number (1, 2, or 3) is unlocked.
    /// Stage numbers outside 1–3 always return false.
    /// </summary>
    public bool IsUnlocked(int stageNumber)
    {
        return stageNumber switch
        {
            1 => Stage1Unlocked,
            2 => Stage2Unlocked,
            3 => Stage3Unlocked,
            4 => TutorialUnlocked,
            _ => false
        };
    }


    /// <summary>
    /// Sets the given stage number as unlocked.
    /// Call Sc_StageSaveData.Save() after this to persist the change.
    /// Stage numbers outside 1–4 are silently ignored.
    /// </summary>
    public void Unlock(int stageNumber)
    {
        switch (stageNumber)
        {
            case 1: Stage1Unlocked = true; break;
            case 2: Stage2Unlocked = true; break;
            case 3: Stage3Unlocked = true; break;
            case 4: TutorialUnlocked = true; break;
            default:
                Debug.LogWarning($"[Sc_StageSaveData] Unlock called with invalid " +
                                 $"stage number: {stageNumber}. Must be 1–4.");
                break;
        }
    }


    // -------------------------------------------------------------------------
    // Static Save / Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads stage save data from disk.
    /// Returns a fresh default save (Stage 1 unlocked) if no file exists yet.
    /// Stage1Unlocked is always forced to true as a safety guarantee —
    /// a corrupted file should never lock the player out of Stage 1.
    /// </summary>
    public static Sc_StageSaveData Load()
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.Log("[Sc_StageSaveData] No save file found — returning fresh save data.");
            return new Sc_StageSaveData();
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            Sc_StageSaveData data = JsonUtility.FromJson<Sc_StageSaveData>(json);

            // Safety guarantee — Stage 1 must always be accessible
            data.Stage1Unlocked = true;
            data.TutorialUnlocked = true;

            Debug.Log($"[Sc_StageSaveData] Loaded stage save. " +
                      $"S1:{data.Stage1Unlocked} S2:{data.Stage2Unlocked} S3:{data.Stage3Unlocked} T:{data.TutorialUnlocked}");

            return data;
        }
        catch (Exception e)
        {
            // If the file is corrupted, return a fresh save rather than crashing.
            // Stage 1 is still accessible — the player loses only Stage 2/3 unlock state.
            // TODO: Consider backing up the corrupted file before overwriting,
            //       same pattern as the almanac save, so a one-time error doesn't
            //       permanently erase completion progress.
            Debug.LogError($"[Sc_StageSaveData] Failed to load save file: {e.Message}. " +
                           $"Returning fresh save data.");
            return new Sc_StageSaveData();
        }
    }


    /// <summary>
    /// Writes the given save data to disk as JSON.
    /// Call this any time Stage2Unlocked or Stage3Unlocked changes.
    /// </summary>
    public static void Save(Sc_StageSaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SaveFilePath, json);

            Debug.Log($"[Sc_StageSaveData] Stage save written to: {SaveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Sc_StageSaveData] Failed to write save file: {e.Message}");
        }
    }


    /// <summary>
    /// Deletes the save file and returns a fresh default.
    /// Use for debug resets — never expose to players.
    /// </summary>
    public static Sc_StageSaveData Reset()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            Debug.Log("[Sc_StageSaveData] Stage save file deleted.");
        }

        return new Sc_StageSaveData();
    }
}