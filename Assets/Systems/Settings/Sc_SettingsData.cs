// Sc_SettingsData.cs
// Serializable save data for all user settings.
// Persisted to disk via JsonUtility — same pattern as Sc_AlmanacSaveData.

using System;
using UnityEngine;

[Serializable]
public class Sc_SettingsData
{
    // ── Audio ─────────────────────────────────────────────────────────────
    [Range(0f, 1f)] public float MasterVolume = 1f;
    [Range(0f, 1f)] public float MusicVolume = 1f;
    [Range(0f, 1f)] public float SFXVolume = 1f;

    // ── Display ───────────────────────────────────────────────────────────
    public bool IsFullscreen = true;
    public int GraphicsQuality = 2;   // 0 = Low, 1 = Medium, 2 = High

    // ── Save / Load ───────────────────────────────────────────────────────
    private const string SAVE_KEY = "ForestGuardians_Settings";

    public static Sc_SettingsData Load()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
        {
            Debug.Log("[Sc_SettingsData] No saved settings found — using defaults.");
            return new Sc_SettingsData();
        }

        try
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            Sc_SettingsData data = JsonUtility.FromJson<Sc_SettingsData>(json);
            Debug.Log("[Sc_SettingsData] Settings loaded.");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Sc_SettingsData] Failed to load settings: {e.Message}. Using defaults.");
            return new Sc_SettingsData();
        }
    }

    public static void Save(Sc_SettingsData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
            Debug.Log("[Sc_SettingsData] Settings saved.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Sc_SettingsData] Failed to save settings: {e.Message}");
        }
    }

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        Debug.Log("[Sc_SettingsData] Settings cleared.");
    }
}