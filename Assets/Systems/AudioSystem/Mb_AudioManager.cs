// Mb_AudioManager.cs
// Central audio playback manager for Forest Guardians.
// Create one instance in Bootstrap and keep it alive across scene loads.
//
// AUDIO MIXER:
//   An AudioMixer gives us three independent volume buses — Music, SFX, and UI.
//   This means the settings screen can adjust each channel separately without
//   touching every AudioSource manually. It also lets Unity's mixer handle
//   ducking and compression per-group if needed later.
//
// SFX POOL:
//   Frequent sounds (hits, ability casts, CuBot deaths) can fire many times per
//   second. Instantiating a new AudioSource each time would generate garbage and
//   stutter. Instead, we pre-warm a fixed pool of AudioSources in Awake() and
//   hand them out round-robin — zero runtime allocation.
//
// STATIC API:
//   Mb_AudioManager.PlayMusic(MusicTrack.Combat_Stage1);
//   Mb_AudioManager.PlaySFX(CombatSFX.Ability_Q);
//   Mb_AudioManager.PlayUI(UISFX.UI_Click);
//
// INSPECTOR SETUP:
//   1. Create a persistent GameObject in your bootstrap scene (e.g. "AudioManager").
//   2. Attach this script and assign all [SerializeField] fields:
//        - Audio Library        → your SO_AudioLibrary asset
//        - Music Mixer Group    → the "Music" group from your AudioMixer
//        - SFX Mixer Group      → the "SFX" group
//        - UI Mixer Group       → the "UI" group
//        - SFX Pool Size        → default 10 is fine for prototype
//        - Crossfade Duration   → default 1.5s feels natural
//        - Current Stage Index  → set to 1, 2, or 3 per scene
//   3. The AudioMixer asset must expose three float parameters named exactly:
//        "MusicVolume", "SFXVolume", "UIVolume"
//      (Right-click each group's Volume in the Mixer → Expose Parameter, then rename it.)

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class Mb_AudioManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static Mb_AudioManager Instance { get; private set; }


    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Audio Library")]
    [SerializeField] private SO_AudioLibrary _AudioLibrary;

    [Header("AudioMixer Groups")]
    // WHY SEPARATE GROUPS: each group is an independent volume bus on the mixer.
    // Assigning a group to an AudioSource routes its output through that bus,
    // so SetMusicVolume() only affects music — never SFX or UI.
    [SerializeField] private AudioMixerGroup _MusicMixerGroup;
    [SerializeField] private AudioMixerGroup _SFXMixerGroup;
    [SerializeField] private AudioMixerGroup _UIMixerGroup;

    [Header("Music Settings")]
    [SerializeField] private float _CrossfadeDuration = 1.5f;

    [Header("SFX Pool")]
    [SerializeField] private int _SFXPoolSize = 10;

    [Header("Stage Index")]
    // Set this in the Inspector for each stage scene.
    // 1 = Stage 1 (Combat_Stage1), 2 = Stage 2, 3 = Stage 3.
    // Mb_AudioManager has no direct reference to Mb_StageManager, so this field
    // is the explicit, rename-safe way to tell the audio manager which track to play.
    [SerializeField] private int _CurrentStageIndex = 1;


    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    // Two music sources enable crossfading — Source A fades out while Source B fades in.
    // After each crossfade, the roles swap so the next fade always works the same way.
    private AudioSource _musicSourceA;
    private AudioSource _musicSourceB;
    private bool _isMusicOnSourceA = true;  // Which source is currently the "active" one

    // The coroutine handle for an in-progress crossfade.
    // Stored so we can cancel it cleanly if a new music request arrives mid-fade.
    private Coroutine _crossfadeCoroutine;

    // Round-robin SFX pool — pre-warmed at Awake(), never allocated at runtime
    private AudioSource[] _sfxPool;
    private int _sfxPoolIndex = 0;

    // Separate single source for UI sounds — UI bleeps should never be cut off
    // by the round-robin SFX pool, and they don't overlap in a way that needs pooling
    private AudioSource _uiSource;

    // AudioMixer parameter name constants — must match the exposed parameter names
    // in the AudioMixer asset exactly. If you rename them there, update these too.
    private const string MIXER_MUSIC_PARAM = "MusicVolume";
    private const string MIXER_SFX_PARAM = "SFXVolume";
    private const string MIXER_UI_PARAM = "UIVolume";

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Standard singleton guard — only one AudioManager should ever exist
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildAudioSources();
        ValidateLibrary();
    }


    private void OnEnable()
    {
        // Subscribe to game events that drive automatic audio changes.
        // All subscriptions live here so unsubscribing in OnDisable is symmetric.
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        Mb_StageManager.OnStageStart += HandleStageStart;
        Mb_WaveManager.OnWaveStart += HandleWaveStart;
        Mb_WaveManager.OnWaveEnd += HandleWaveEnd;
        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath;
        MB_CuBotBase.OnCuBotSpawn += HandleCuBotSpawn;
        //Sc_BaseAbility.OnCriticalHit += HandleCriticalHit;
        Mb_PauseManager.OnPaused += HandlePaused;
        Mb_PauseManager.OnResumed += HandleResumed;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;

        Mb_StageManager.OnStageStart -= HandleStageStart;
        Mb_WaveManager.OnWaveStart -= HandleWaveStart;
        Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;
        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;
        MB_CuBotBase.OnCuBotSpawn -= HandleCuBotSpawn;
        //Sc_BaseAbility.OnCriticalHit -= HandleCriticalHit;
        Mb_PauseManager.OnPaused -= HandlePaused;
        Mb_PauseManager.OnResumed -= HandleResumed;
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Initialization Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildAudioSources()
    {
        // Two dedicated music sources for crossfading — added as components on
        // this same GameObject so they persist across scenes with us
        _musicSourceA = gameObject.AddComponent<AudioSource>();
        _musicSourceA.outputAudioMixerGroup = _MusicMixerGroup;
        _musicSourceA.loop = true;
        _musicSourceA.playOnAwake = false;

        _musicSourceB = gameObject.AddComponent<AudioSource>();
        _musicSourceB.outputAudioMixerGroup = _MusicMixerGroup;
        _musicSourceB.loop = true;
        _musicSourceB.playOnAwake = false;

        // Single dedicated UI source — non-looping, non-pooled
        _uiSource = gameObject.AddComponent<AudioSource>();
        _uiSource.outputAudioMixerGroup = _UIMixerGroup;
        _uiSource.loop = false;
        _uiSource.playOnAwake = false;

        // Pre-warm the SFX pool — all sources created here, never at runtime.
        // Round-robin index wraps around so the oldest source gets reused first.
        _sfxPool = new AudioSource[_SFXPoolSize];
        for (int i = 0; i < _SFXPoolSize; i++)
        {
            _sfxPool[i] = gameObject.AddComponent<AudioSource>();
            _sfxPool[i].outputAudioMixerGroup = _SFXMixerGroup;
            _sfxPool[i].loop = false;
            _sfxPool[i].playOnAwake = false;

            // Switch to 3D — spatialBlend 0 = fully 2D, 1 = fully 3D
            _sfxPool[i].spatialBlend = 1f;

            // How quickly the sound fades with distance.
            // Logarithmic roll-off matches how sound behaves in real space.
            // Min distance = full volume within this radius.
            // Max distance = silence beyond this radius.
            // TODO: Tune these to match your scene scale.
            _sfxPool[i].rolloffMode = AudioRolloffMode.Logarithmic;
            _sfxPool[i].minDistance = 5f;
            _sfxPool[i].maxDistance = 40f;
        }
    }


    private void ValidateLibrary()
    {
        if (_AudioLibrary == null)
        {
            Debug.LogError("[Mb_AudioManager] No SO_AudioLibrary assigned! " +
                           "Drag the AudioLibrary asset into the Inspector field.");
            return;
        }

        // Run the startup validation — logs a warning for every missing clip
        var loader = new Sc_AudioLibraryLoader(_AudioLibrary);
        loader.Validate();
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Public Static API
    // Any script can call these without holding a reference to Mb_AudioManager.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays the given music track. If crossfade is true (default), the current
    /// track fades out as the new one fades in over _CrossfadeDuration seconds.
    /// If crossfade is false, the switch is instant — useful for Victory/Defeat
    /// stings that should cut over immediately.
    /// </summary>
    public static void PlayMusic(MusicTrack track, bool crossfade = true)
    {
        if (Instance == null) return;
        Instance.PlayMusicInternal(track, crossfade);
    }

    /// <summary>Plays a combat SFX clip from the pool. Safe to call every frame.</summary>
    public static void PlaySFX(CombatSFX sfx, Vector3? worldPosition = null)
    {
        if (Instance == null) return;
        Instance.PlaySFXInternal(sfx, worldPosition ?? Instance.transform.position);
    }

    /// <summary>Plays an environment SFX clip from the shared SFX pool.</summary>
    public static void PlayEnvironmentSFX(EnvironmentSFX sfx, Vector3? worldPosition = null)
    {
        if (Instance == null) return;
        Instance.PlaySFXInternal(sfx, worldPosition ?? Instance.transform.position);
    }

    /// <summary>Plays a UI SFX clip. Uses a dedicated source — never cut off by combat SFX.</summary>
    public static void PlayUI(UISFX sfx, Vector3? worldPosition = null)
    {
        if (Instance == null) return;
        Instance.PlayUIInternal(sfx);
    }

    /// <summary>Stops music immediately without a fade.</summary>
    public static void StopMusic()
    {
        if (Instance == null) return;
        Instance.StopMusicInternal();
    }

    /// <summary>
    /// Sets music bus volume. Range 0–1 is converted to decibels for the AudioMixer.
    /// A value of 0 is silenced (mapped to -80 dB). A value of 1 is full volume (0 dB).
    /// </summary>
    public static void SetMusicVolume(float volume)
    {
        if (Instance == null) return;
        Instance.SetMixerVolume(MIXER_MUSIC_PARAM, volume);
    }

    /// <summary>Sets SFX bus volume. Same 0–1 range as SetMusicVolume.</summary>
    public static void SetSFXVolume(float volume)
    {
        if (Instance == null) return;
        Instance.SetMixerVolume(MIXER_SFX_PARAM, volume);
    }

    /// <summary>Sets UI SFX bus volume. Same 0–1 range as SetMusicVolume.</summary>
    public static void SetUIVolume(float volume)
    {
        if (Instance == null) return;
        Instance.SetMixerVolume(MIXER_UI_PARAM, volume);
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Internal Playback Logic
    // ─────────────────────────────────────────────────────────────────────────

    private void PlayMusicInternal(MusicTrack track, bool crossfade)
    {
        if (_AudioLibrary == null) return;

        // MusicTrack.None is the "stop" signal — treat it as StopMusic()
        if (track == MusicTrack.None)
        {
            StopMusicInternal();
            return;
        }

        if (!_AudioLibrary.TryGetMusic(track, out MusicEntry entry) || entry.Clip == null)
        {
            Debug.LogWarning($"[Mb_AudioManager] PlayMusic: no clip assigned for MusicTrack.{track}. Skipping.");
            return;
        }

        // If a crossfade is already running, cancel it cleanly before starting a new one.
        // This prevents orphaned fades that would keep adjusting volume after the fact.
        if (_crossfadeCoroutine != null)
        {
            StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = null;

            // Snap both sources to their final crossfade state so volumes are
            // consistent before the new fade begins — no leftover mid-fade values
            GetActiveSource().volume = 0f;
            GetInactiveSource().volume = 1f;
        }

        if (crossfade)
        {
            _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(entry));
        }
        else
        {
            // Instant switch — stop everything and play directly on Source A
            _musicSourceA.Stop();
            _musicSourceB.Stop();
            _musicSourceA.clip = entry.Clip;
            _musicSourceA.volume = entry.DefaultVolume;
            _musicSourceA.Play();
            _isMusicOnSourceA = true;
        }
    }


    private void PlaySFXInternal(CombatSFX sfx, Vector3 worldPosition)
    {
        if (_AudioLibrary == null) return;

        if (!_AudioLibrary.TryGetCombatSFX(sfx, out CombatSFXEntry entry) || entry.Clip == null)
        {
            Debug.LogWarning($"[Mb_AudioManager] PlaySFX: no clip for CombatSFX.{sfx}. Skipping.");
            return;
        }

        AudioSource source = _sfxPool[_sfxPoolIndex];
        _sfxPoolIndex = (_sfxPoolIndex + 1) % _SFXPoolSize;

        // Move the AudioSource to the sound's origin before playing.
        // Since the AudioManager GameObject is persistent and never moves,
        // we reposition the source component's transform each time.
        source.transform.position = worldPosition;

        source.clip = entry.Clip;
        source.volume = entry.DefaultVolume;
        source.Play();
    }

    private void PlaySFXInternal(EnvironmentSFX sfx, Vector3 worldPosition)
    {
        if (_AudioLibrary == null) return;

        if (!_AudioLibrary.TryGetEnvironmentSFX(sfx, out EnvironmentSFXEntry entry) || entry.Clip == null)
        {
            Debug.LogWarning($"[Mb_AudioManager] PlaySFX: no clip for EnvironmentSFX.{sfx}. Skipping.");
            return;
        }

        AudioSource source = _sfxPool[_sfxPoolIndex];
        _sfxPoolIndex = (_sfxPoolIndex + 1) % _SFXPoolSize;

        // Move the AudioSource to the sound's origin before playing.
        // Since the AudioManager GameObject is persistent and never moves,
        // we reposition the source component's transform each time.
        source.transform.position = worldPosition;

        source.clip = entry.Clip;
        source.volume = entry.DefaultVolume;
        source.Play();
    }


    private void PlayUIInternal(UISFX sfx)
    {
        if (_AudioLibrary == null) return;

        if (!_AudioLibrary.TryGetUISFX(sfx, out UISFXEntry entry) || entry.Clip == null)
        {
            Debug.LogWarning($"[Mb_AudioManager] PlayUI: no clip assigned for UISFX.{sfx}. Skipping.");
            return;
        }

        // PlayOneShot lets the UI source play a new clip without cutting off
        // the previous one — good for rapid button clicks
        _uiSource.PlayOneShot(entry.Clip, entry.DefaultVolume);
    }


    private void StopMusicInternal()
    {
        if (_crossfadeCoroutine != null)
        {
            StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = null;
        }

        _musicSourceA.Stop();
        _musicSourceB.Stop();
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Crossfade Coroutine
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator CrossfadeRoutine(MusicEntry incoming)
    {
        // The "incoming" source is whichever is NOT currently playing
        AudioSource outgoing = GetActiveSource();
        AudioSource incomingSource = GetInactiveSource();

        // Start the new track silently on the inactive source
        incomingSource.clip = incoming.Clip;
        incomingSource.volume = 0f;
        incomingSource.Play();

        float elapsed = 0f;
        float startVolume = outgoing.volume;         // Fade FROM this
        float targetVolume = incoming.DefaultVolume; // Fade TO this

        while (elapsed < _CrossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled so crossfade works while paused
            float t = Mathf.Clamp01(elapsed / _CrossfadeDuration);

            outgoing.volume = Mathf.Lerp(startVolume, 0f, t);
            incomingSource.volume = Mathf.Lerp(0f, targetVolume, t);

            yield return null;
        }

        // Snap to final values and stop the outgoing source cleanly
        outgoing.volume = 0f;
        outgoing.Stop();
        outgoing.clip = null; // Release the clip reference — good hygiene

        incomingSource.volume = targetVolume;

        // Swap the active flag so the next crossfade knows which source is playing
        _isMusicOnSourceA = !_isMusicOnSourceA;
        _crossfadeCoroutine = null;
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Volume Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void SetMixerVolume(string parameterName, float volume)
    {
        if (_MusicMixerGroup == null) return;

        // AudioMixer uses decibels. The formula below converts a 0–1 slider value
        // into the -80 dB to 0 dB range that mixers expect.
        // We clamp volume to 0.0001f as a floor because Mathf.Log10(0) is -infinity.
        float dB = Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;
        _MusicMixerGroup.audioMixer.SetFloat(parameterName, dB);
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Source Swap Helpers
    // ─────────────────────────────────────────────────────────────────────────

    // Returns whichever music source is currently the "active" (playing) one
    private AudioSource GetActiveSource() => _isMusicOnSourceA ? _musicSourceA : _musicSourceB;

    // Returns whichever music source is currently idle — the one we fade IN to
    private AudioSource GetInactiveSource() => _isMusicOnSourceA ? _musicSourceB : _musicSourceA;


    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers — Game-Driven Audio
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.MainMenu:
                // Player returned to the main menu — play the menu track with a crossfade
                PlayMusicInternal(MusicTrack.MainMenu, crossfade: true);
                break;

            case GameState.Victory:
                // Victory sting should cut over immediately — no crossfade
                PlayMusicInternal(MusicTrack.Victory, crossfade: false);
                break;

            case GameState.Defeat:
                PlayMusicInternal(MusicTrack.Defeat, crossfade: false);
                break;

                // LoadingStage, Playing, RewardsPanel, Paused:
                // Music is handled by HandleStageStart and HandlePaused/HandleResumed below.
                // We intentionally do nothing here for those states to avoid double-switching.
        }
    }


    private void HandleStageStart()
    {
        // Convert the Inspector-assigned stage index to the correct music enum value.
        // This is the one place where _CurrentStageIndex drives the music decision.
        MusicTrack stageTrack = _CurrentStageIndex switch
        {
            1 => MusicTrack.Combat_Stage1,
            2 => MusicTrack.Combat_Stage2,
            3 => MusicTrack.Combat_Stage3,
            _ => MusicTrack.Combat_Stage1 // Fallback — log a warning below
        };

        if (_CurrentStageIndex < 1 || _CurrentStageIndex > 3)
        {
            Debug.LogWarning($"[Mb_AudioManager] _CurrentStageIndex is {_CurrentStageIndex} — " +
                             "expected 1, 2, or 3. Defaulting to Combat_Stage1. " +
                             "Set the correct index in the Inspector.");
        }

        PlayMusicInternal(stageTrack, crossfade: true);
    }


    private void HandleWaveStart(int waveIndex)
    {
        PlayUIInternal(UISFX.UI_WaveStart);
    }


    private void HandleWaveEnd(int waveIndex)
    {
        PlayUIInternal(UISFX.UI_WaveComplete);
    }


    private void HandleCuBotDeath(GameObject deadCuBot)
    {
        Vector3 pos = deadCuBot.transform.position;
        PlaySFXInternal(CombatSFX.CuBot_Death, pos);
    }


    private void HandleCuBotSpawn(GameObject cuBot)
    {
        Vector3 pos = cuBot.transform.position;
        PlaySFXInternal(CombatSFX.CuBot_Spawn, pos);
    }


    //private void HandleCriticalHit(float critDamage, Mb_CharacterBase attacker)
    //{
    //    PlaySFXInternal(CombatSFX.);
    //}


    private void HandlePaused()
    {
        // Pause all AudioSources on this GameObject — music, SFX pool, and UI source.
        // AudioListener.pause (set in Mb_PauseManager) already silences all audio globally,
        // but pausing the sources explicitly ensures clip positions are preserved so
        // music resumes exactly where it left off.
        _musicSourceA.Pause();
        _musicSourceB.Pause();
        _uiSource.Pause();

        foreach (AudioSource source in _sfxPool)
            source.Pause();
    }


    private void HandleResumed()
    {
        _musicSourceA.UnPause();
        _musicSourceB.UnPause();
        _uiSource.UnPause();

        foreach (AudioSource source in _sfxPool)
            source.UnPause();
    }
}
