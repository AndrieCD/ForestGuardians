// Sc_AlmanacSaveData.cs
// Serializable save data class for the Wildlife Almanac.
// Tracks which entries are unlocked and how many times each has been completed.
//
// WHY A LIST INSTEAD OF A DICTIONARY:
//   JsonUtility cannot serialize Dictionary<K,V> directly. We use a List of
//   Sc_SaveEntry structs (key-value pairs) as a workaround — the same pattern
//   used elsewhere in this project for JSON persistence.
//   GetCount() and SetCount() hide this detail from all callers.
//
// SAVE FILE LOCATION:
//   Application.persistentDataPath/almanac_save.json
//   On Windows: %AppData%/../LocalLow/<Company>/<Product>/almanac_save.json
//
// USAGE:
//   // Load (call once on startup):
//   Sc_AlmanacSaveData save = Sc_AlmanacSaveData.Load();
//
//   // Check if unlocked:
//   bool unlocked = save.UnlockedEntries.Contains("Philippine Eagle");
//
//   // Read completion count:
//   int count = save.GetCount("Philippine Eagle");
//
//   // Write completion count and save:
//   save.SetCount("Philippine Eagle", count + 1);
//   Sc_AlmanacSaveData.Save(save);

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class Sc_AlmanacSaveData
{
    // -------------------------------------------------------------------------
    // Save File Path
    // -------------------------------------------------------------------------

    // TODO: Confirm this filename with the team before shipping.
    // Changing it after players have saves will invalidate their progress.
    private const string SAVE_FILE_NAME = "almanac_save.json";

    private static string SaveFilePath =>
        Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);


    // -------------------------------------------------------------------------
    // Serialized Data
    // -------------------------------------------------------------------------

    // CommonName strings of every fully unlocked entry.
    // We use CommonName as the key because it matches SO_WildlifeEntry.CommonName
    // and is stable across sessions — SO instance IDs are not.
    public List<string> UnlockedEntries = new List<string>();

    // How many times each entry has been fully completed (first unlock = 1,
    // each repeat run completion increments by 1).
    // Stored as a list of key-value pairs because JsonUtility cannot
    // serialize Dictionary<string, int> directly.
    public List<Sc_SaveEntry> CompletionCounts = new List<Sc_SaveEntry>();


    // -------------------------------------------------------------------------
    // Completion Count Helpers
    // These let callers treat CompletionCounts like a dictionary without
    // knowing about the List<Sc_SaveEntry> workaround underneath.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the number of times the entry with the given CommonName
    /// has been fully completed. Returns 0 if no record exists yet.
    /// </summary>
    public int GetCount(string commonName)
    {
        // Linear search is fine here — 13 entries maximum, called rarely
        foreach (Sc_SaveEntry entry in CompletionCounts)
        {
            if (entry.Key == commonName)
                return entry.Value;
        }
        return 0;
    }


    /// <summary>
    /// Sets the completion count for the given CommonName.
    /// Updates the existing entry if one exists, or adds a new one.
    /// Call Sc_AlmanacSaveData.Save() after this to persist the change.
    /// </summary>
    public void SetCount(string commonName, int count)
    {
        // Try to update an existing entry first
        for (int i = 0; i < CompletionCounts.Count; i++)
        {
            if (CompletionCounts[i].Key == commonName)
            {
                // Sc_SaveEntry is a struct — must replace the whole element,
                // not mutate a field on a copy
                CompletionCounts[i] = new Sc_SaveEntry(commonName, count);
                return;
            }
        }

        // No existing entry found — add a new one
        CompletionCounts.Add(new Sc_SaveEntry(commonName, count));
    }


    /// <summary>
    /// Convenience: returns true if the entry with the given CommonName
    /// has been unlocked at least once.
    /// </summary>
    public bool IsUnlocked(string commonName)
    {
        return UnlockedEntries.Contains(commonName);
    }


    // -------------------------------------------------------------------------
    // Static Save / Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads almanac save data from disk.
    /// Returns a fresh empty save if no file exists yet (first launch).
    /// </summary>
    public static Sc_AlmanacSaveData Load()
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.Log("[Sc_AlmanacSaveData] No save file found — returning fresh save data.");
            return new Sc_AlmanacSaveData();
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            Sc_AlmanacSaveData data = JsonUtility.FromJson<Sc_AlmanacSaveData>(json);

            Debug.Log($"[Sc_AlmanacSaveData] Loaded almanac save. " +
                      $"Unlocked: {data.UnlockedEntries.Count} entries.");
            return data;
        }
        catch (Exception e)
        {
            // If the file is corrupted, start fresh rather than crashing.
            // TODO: Consider backing up the corrupted file before overwriting
            //       so players don't permanently lose progress from a one-time error.
            Debug.LogError($"[Sc_AlmanacSaveData] Failed to load save file: {e.Message}. " +
                           $"Returning fresh save data.");
            return new Sc_AlmanacSaveData();
        }
    }


    /// <summary>
    /// Writes the given save data to disk as JSON.
    /// Call this any time UnlockedEntries or CompletionCounts changes.
    /// </summary>
    public static void Save(Sc_AlmanacSaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SaveFilePath, json);

            Debug.Log($"[Sc_AlmanacSaveData] Almanac save written to: {SaveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Sc_AlmanacSaveData] Failed to write save file: {e.Message}");
        }
    }


    /// <summary>
    /// Deletes the save file from disk and returns a fresh empty save.
    /// Use this for debug resets — not exposed to players.
    /// </summary>
    public static Sc_AlmanacSaveData Reset()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            Debug.Log("[Sc_AlmanacSaveData] Almanac save file deleted.");
        }

        return new Sc_AlmanacSaveData();
    }
}


// -------------------------------------------------------------------------
// Sc_SaveEntry — key-value pair struct for JsonUtility-compatible storage
// -------------------------------------------------------------------------

/// <summary>
/// A serializable key-value pair used in place of Dictionary<string, int>
/// because JsonUtility cannot serialize dictionaries directly.
/// Used by Sc_AlmanacSaveData.CompletionCounts.
/// </summary>
[Serializable]
public struct Sc_SaveEntry
{
    public string Key;
    public int Value;

    public Sc_SaveEntry(string key, int value)
    {
        Key = key;
        Value = value;
    }
}