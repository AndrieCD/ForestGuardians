// AudioVFXLibraryBackupTool.cs
// Editor-only backup utility for SO_AudioLibrary and SO_VFXLibrary assignments.
//
// Menu:
//   Tools > Forest Guardians > Backups > Export Selected Audio/VFX Libraries
//   Tools > Forest Guardians > Backups > Export All Audio/VFX Libraries
//
// Output:
//   Assets/EditorBackups/AudioVFXLibraryBackups/*.json
//
// Use this before refactoring audio/VFX enums or ScriptableObject entry structs.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AudioVFXLibraryBackupTool
{
    private const string BACKUP_FOLDER = "Assets/EditorBackups/AudioVFXLibraryBackups";

    [MenuItem("Tools/Forest Guardians/Backups/Export Selected Audio-VFX Libraries")]
    public static void ExportSelectedLibraries()
    {
        var audioLibraries = new List<SO_AudioLibrary>();
        var vfxLibraries = new List<SO_VFXLibrary>();

        foreach (UnityEngine.Object selectedObject in Selection.objects)
        {
            if (selectedObject is SO_AudioLibrary audioLibrary)
                audioLibraries.Add(audioLibrary);

            if (selectedObject is SO_VFXLibrary vfxLibrary)
                vfxLibraries.Add(vfxLibrary);
        }

        if (audioLibraries.Count == 0 && vfxLibraries.Count == 0)
        {
            Debug.LogWarning("[AudioVFXLibraryBackupTool] Select one or more SO_AudioLibrary or SO_VFXLibrary assets first.");
            return;
        }

        ExportLibraries(audioLibraries, vfxLibraries);
    }

    [MenuItem("Tools/Forest Guardians/Backups/Export All Audio-VFX Libraries")]
    public static void ExportAllLibraries()
    {
        ExportLibraries(FindAssetsOfType<SO_AudioLibrary>(), FindAssetsOfType<SO_VFXLibrary>());
    }

    private static void ExportLibraries(List<SO_AudioLibrary> audioLibraries, List<SO_VFXLibrary> vfxLibraries)
    {
        EnsureBackupFolderExists();

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        int exportedCount = 0;

        foreach (SO_AudioLibrary library in audioLibraries)
        {
            AudioLibraryBackup backup = BuildAudioBackup(library);
            string fileName = $"{timestamp}_{SanitizeFileName(library.name)}_AudioLibraryBackup.json";
            WriteJson(fileName, backup);
            exportedCount++;
        }

        foreach (SO_VFXLibrary library in vfxLibraries)
        {
            VFXLibraryBackup backup = BuildVFXBackup(library);
            string fileName = $"{timestamp}_{SanitizeFileName(library.name)}_VFXLibraryBackup.json";
            WriteJson(fileName, backup);
            exportedCount++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[AudioVFXLibraryBackupTool] Exported {exportedCount} backup file(s) to {BACKUP_FOLDER}.");
    }

    private static AudioLibraryBackup BuildAudioBackup(SO_AudioLibrary library)
    {
        var backup = new AudioLibraryBackup
        {
            AssetName = library.name,
            AssetPath = AssetDatabase.GetAssetPath(library),
            ExportedAt = DateTime.Now.ToString("O")
        };

        foreach (MusicEntry entry in library.MusicTracks)
            backup.MusicTracks.Add(BuildAudioEntry("MusicTracks", entry.Track.ToString(), (int)entry.Track, entry.Clip, entry.DefaultVolume));

        foreach (CombatSFXEntry entry in library.CombatSounds)
            backup.CombatSounds.Add(BuildAudioEntry("CombatSounds", entry.Key.ToString(), (int)entry.Key, entry.Clip, entry.DefaultVolume));

        foreach (EnvironmentSFXEntry entry in library.EnvironmentSounds)
            backup.EnvironmentSounds.Add(BuildAudioEntry("EnvironmentSounds", entry.Key.ToString(), (int)entry.Key, entry.Clip, entry.DefaultVolume));

        foreach (UISFXEntry entry in library.UISounds)
            backup.UISounds.Add(BuildAudioEntry("UISounds", entry.Key.ToString(), (int)entry.Key, entry.Clip, entry.DefaultVolume));

        return backup;
    }

    private static VFXLibraryBackup BuildVFXBackup(SO_VFXLibrary library)
    {
        var backup = new VFXLibraryBackup
        {
            AssetName = library.name,
            AssetPath = AssetDatabase.GetAssetPath(library),
            ExportedAt = DateTime.Now.ToString("O")
        };

        AddVFXEntries(backup.GenericCombatVFX, "GenericCombatVFX", library.GenericCombatVFX,
            entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize);

        AddVFXEntries(backup.RajahVFX, "RajahVFX", library.RajahVFX,
            entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize);

        AddVFXEntries(backup.MariVFX, "MariVFX", library.MariVFX,
            entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize);

        AddVFXEntries(backup.CuBotCombatVFX, "CuBotCombatVFX", library.CuBotCombatVFX,
            entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize);

        AddVFXEntries(backup.StatusEffectVFX, "StatusEffectVFX", library.StatusEffectVFX,
            entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize);

        AddVFXEntries(backup.EnvironmentVFX, "EnvironmentVFX", library.EnvironmentVFX,
            entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize);

        AddVFXEntries(backup.FootstepVFX, "FootstepVFX", library.FootstepVFX,
            entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize);

        AddVFXEntries(backup.UIVFX, "UIVFX", library.UIVFX,
            entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize);

        return backup;
    }

    private static AudioBackupEntry BuildAudioEntry(
        string category,
        string enumName,
        int enumValue,
        AudioClip clip,
        float defaultVolume)
    {
        return new AudioBackupEntry
        {
            Category = category,
            EnumName = enumName,
            EnumValue = enumValue,
            ClipPath = GetAssetPath(clip),
            ClipGuid = GetAssetGuid(clip),
            DefaultVolume = defaultVolume
        };
    }

    private static void AddVFXEntries<TEntry, TEnum>(
        List<VFXBackupEntry> destination,
        string category,
        List<TEntry> source,
        Func<TEntry, TEnum> getType,
        Func<TEntry, GameObject> getPrefab,
        Func<TEntry, float> getLifetime,
        Func<TEntry, int> getPoolSize) where TEnum : Enum
    {
        foreach (TEntry entry in source)
        {
            TEnum type = getType(entry);
            GameObject prefab = getPrefab(entry);

            destination.Add(new VFXBackupEntry
            {
                Category = category,
                EnumName = type.ToString(),
                EnumValue = Convert.ToInt32(type),
                PrefabPath = GetAssetPath(prefab),
                PrefabGuid = GetAssetGuid(prefab),
                Lifetime = getLifetime(entry),
                PoolSize = getPoolSize(entry)
            });
        }
    }

    private static List<T> FindAssetsOfType<T>() where T : UnityEngine.Object
    {
        var results = new List<T>();
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
                results.Add(asset);
        }

        return results;
    }

    private static string GetAssetPath(UnityEngine.Object asset)
    {
        return asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;
    }

    private static string GetAssetGuid(UnityEngine.Object asset)
    {
        if (asset == null) return string.Empty;

        string path = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
    }

    private static void EnsureBackupFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/EditorBackups"))
            AssetDatabase.CreateFolder("Assets", "EditorBackups");

        if (!AssetDatabase.IsValidFolder(BACKUP_FOLDER))
            AssetDatabase.CreateFolder("Assets/EditorBackups", "AudioVFXLibraryBackups");
    }

    private static void WriteJson<T>(string fileName, T data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        string path = Path.Combine(BACKUP_FOLDER, fileName);
        File.WriteAllText(path, json);
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalidChar, '_');

        return fileName;
    }
}

[Serializable]
public class AudioLibraryBackup
{
    public string AssetName;
    public string AssetPath;
    public string ExportedAt;
    public List<AudioBackupEntry> MusicTracks = new List<AudioBackupEntry>();
    public List<AudioBackupEntry> CombatSounds = new List<AudioBackupEntry>();
    public List<AudioBackupEntry> EnvironmentSounds = new List<AudioBackupEntry>();
    public List<AudioBackupEntry> UISounds = new List<AudioBackupEntry>();
}

[Serializable]
public class AudioBackupEntry
{
    public string Category;
    public string EnumName;
    public int EnumValue;
    public string ClipPath;
    public string ClipGuid;
    public float DefaultVolume;
}

[Serializable]
public class VFXLibraryBackup
{
    public string AssetName;
    public string AssetPath;
    public string ExportedAt;
    public List<VFXBackupEntry> GenericCombatVFX = new List<VFXBackupEntry>();
    public List<VFXBackupEntry> RajahVFX = new List<VFXBackupEntry>();
    public List<VFXBackupEntry> MariVFX = new List<VFXBackupEntry>();
    public List<VFXBackupEntry> CuBotCombatVFX = new List<VFXBackupEntry>();
    public List<VFXBackupEntry> StatusEffectVFX = new List<VFXBackupEntry>();
    public List<VFXBackupEntry> EnvironmentVFX = new List<VFXBackupEntry>();
    public List<VFXBackupEntry> FootstepVFX = new List<VFXBackupEntry>();
    public List<VFXBackupEntry> UIVFX = new List<VFXBackupEntry>();
}

[Serializable]
public class VFXBackupEntry
{
    public string Category;
    public string EnumName;
    public int EnumValue;
    public string PrefabPath;
    public string PrefabGuid;
    public float Lifetime;
    public int PoolSize;
}
