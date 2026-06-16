// Mb_SettingsManager.cs
// Persistent singleton — survives scene loads via DontDestroyOnLoad.
// Owns the canonical settings state and applies it to Unity systems.
//
// RESPONSIBILITIES:
//   - Load settings from disk on Awake()
//   - Apply settings to AudioMixer, Screen, and QualitySettings
//   - Expose Apply() so Mb_SettingsUI can push working copies before Save()
//   - Expose Save() / Revert() so the UI's SAVE and REVERT buttons work correctly
//
// USAGE FROM UI:
//   // When a slider moves:
//   Mb_SettingsManager.Instance.ApplyAudio(masterVol, musicVol, sfxVol);
//
//   // When SAVE is clicked:
//   Mb_SettingsManager.Instance.Save();
//
//   // When REVERT is clicked:
//   Mb_SettingsManager.Instance.Revert();
//
// AUDIOMIXER SETUP:
//   The AudioMixer must expose three float parameters named exactly:
//     "MasterVolume", "MusicVolume", "SFXVolume"
//   These are the same parameters already used by Mb_AudioManager.
//   Assign the same AudioMixer asset to both managers, or wire them together
//   via the shared Mb_AudioManager.SetMasterVolume / SetMusicVolume / SetSFXVolume calls.

using UnityEngine;
using UnityEngine.Audio;

public class Mb_SettingsManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
    public static Mb_SettingsManager Instance { get; private set; }

    // ── Inspector Fields ──────────────────────────────────────────────────
    [Header("Audio Mixer")]
    [Tooltip("The same AudioMixer asset used by Mb_AudioManager.")]
    [SerializeField] private AudioMixer _AudioMixer;

    // ── Runtime State ─────────────────────────────────────────────────────

    // The last-saved (committed) settings — used for Revert()
    private Sc_SettingsData _saved;

    // The working copy — modified live by UI sliders/toggles before Save()
    private Sc_SettingsData _working;

    // Public read-only access so UI can initialise its controls on open
    public Sc_SettingsData Current => _working;

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _saved = Sc_SettingsData.Load();
        _working = Clone(_saved);

        ApplyAll(_working);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes new audio values into the working copy and applies them immediately
    /// so the player hears changes in real-time while dragging sliders.
    /// </summary>
    public void ApplyAudio(float master, float music, float sfx)
    {
        _working.MasterVolume = master;
        _working.MusicVolume = music;
        _working.SFXVolume = sfx;
        ApplyAudioToMixer(_working);
    }

    /// <summary>
    /// Pushes new display values into the working copy and applies them immediately.
    /// </summary>
    public void ApplyDisplay(bool fullscreen, int quality)
    {
        _working.IsFullscreen = fullscreen;
        _working.GraphicsQuality = quality;
        ApplyDisplaySettings(_working);
    }

    /// <summary>
    /// Commits the working copy to disk. Called when the player clicks SAVE.
    /// </summary>
    public void Save()
    {
        _saved = Clone(_working);
        Sc_SettingsData.Save(_saved);
        Debug.Log("[Mb_SettingsManager] Settings committed and saved.");
    }

    /// <summary>
    /// Discards the working copy and restores the last-saved state.
    /// Called when the player clicks REVERT.
    /// </summary>
    public void Revert()
    {
        _working = Clone(_saved);
        ApplyAll(_working);
        Debug.Log("[Mb_SettingsManager] Settings reverted to last save.");
    }

    // ── Internal Helpers ──────────────────────────────────────────────────

    private void ApplyAll(Sc_SettingsData data)
    {
        ApplyAudioToMixer(data);
        ApplyDisplaySettings(data);
    }

    private void ApplyAudioToMixer(Sc_SettingsData data)
    {
        if (_AudioMixer == null) return;

        SetMixerVolume("MasterVolume", data.MasterVolume);
        SetMixerVolume("MusicVolume", data.MusicVolume);
        SetMixerVolume("SFXVolume", data.SFXVolume);
    }

    // Converts a 0–1 linear value to decibels for the AudioMixer.
    // Identical to the formula already in Mb_AudioManager.
    private void SetMixerVolume(string parameter, float linearVolume)
    {
        float dB = Mathf.Log10(Mathf.Max(linearVolume, 0.0001f)) * 20f;
        _AudioMixer.SetFloat(parameter, dB);
    }

    private void ApplyDisplaySettings(Sc_SettingsData data)
    {
        // Fullscreen / Windowed
        FullScreenMode mode = data.IsFullscreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        Screen.fullScreenMode = mode;

        // Graphics Quality
        // Unity's quality levels must be set up in Project Settings > Quality.
        // Index 0 = lowest (maps to our Low), ascending to highest (our High).
        // TODO: Confirm your project's quality level count matches 0=Low/1=Med/2=High.
        int clampedQuality = Mathf.Clamp(data.GraphicsQuality, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(clampedQuality, applyExpensiveChanges: true);
    }

    // Deep-copies a settings struct so changes to the working copy
    // never silently mutate the saved reference.
    private Sc_SettingsData Clone(Sc_SettingsData source)
    {
        return new Sc_SettingsData
        {
            MasterVolume = source.MasterVolume,
            MusicVolume = source.MusicVolume,
            SFXVolume = source.SFXVolume,
            IsFullscreen = source.IsFullscreen,
            GraphicsQuality = source.GraphicsQuality
        };
    }
}