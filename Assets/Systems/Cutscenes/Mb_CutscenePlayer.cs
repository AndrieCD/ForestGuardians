using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Video;

public class Mb_CutscenePlayer : MonoBehaviour
{
    [Header("Cutscene Data")]
    [SerializeField] private SO_CutsceneVideo[] Cutscenes;

    [Header("Video")]
    [SerializeField] private VideoPlayer VideoPlayer;
    [SerializeField] private RawImage VideoDisplay;

    [Header("Audio")]
    [SerializeField] private AudioSource CutsceneAudioSource;

    [Header("Controls")]
    [SerializeField] private GameObject ControlsPanel;
    [SerializeField] private Button ContinueButton;
    [SerializeField] private Button BackToMainMenuButton;
    [SerializeField] private Button SkipButton;

    private bool _hasFinished;

    private void Awake()
    {
        if (VideoPlayer == null)
            Debug.LogError("[Mb_CutscenePlayer] VideoPlayer is not assigned.");

        if (CutsceneAudioSource == null)
            Debug.LogError("[Mb_CutscenePlayer] CutsceneAudioSource is not assigned.");

        EnsureEventSystem();
        EnsureVideoDoesNotBlockControls();
    }

    private void OnEnable()
    {
        if (VideoPlayer != null)
            VideoPlayer.loopPointReached += HandleVideoFinished;

        ContinueButton?.onClick.RemoveAllListeners();
        ContinueButton?.onClick.AddListener(OnContinueClicked);

        BackToMainMenuButton?.onClick.RemoveAllListeners();
        BackToMainMenuButton?.onClick.AddListener(OnBackToMainMenuClicked);

        SkipButton?.onClick.RemoveAllListeners();
        SkipButton?.onClick.AddListener(OnSkipClicked);
    }

    private void OnDisable()
    {
        if (VideoPlayer != null)
            VideoPlayer.loopPointReached -= HandleVideoFinished;

        StopPlayback();
    }

    private void Start()
    {
        PlayActiveCutscene();
    }


    private void Update()
    {
        if (ShouldSkipFromKeyboard())
            OnSkipClicked();
    }

    private void PlayActiveCutscene()
    {
        if (!Sc_CutsceneSession.HasActiveCutscene)
        {
            Debug.LogError("[Mb_CutscenePlayer] No active cutscene was set before loading CutscenePlayer.");
            SceneLoader.Instance.LoadMainMenu();
            return;
        }

        SO_CutsceneVideo cutscene = GetCutscene(Sc_CutsceneSession.ActiveCutsceneId);
        if (cutscene == null || cutscene.VideoClip == null)
        {
            Debug.LogError($"[Mb_CutscenePlayer] Missing video clip for cutscene " +
                           $"{Sc_CutsceneSession.ActiveCutsceneId}. Continuing to the configured destination.");
            SceneLoader.Instance.ContinueFromActiveCutscene();
            return;
        }

        _hasFinished = false;
        SetControlsVisible(false);
        ConfigureVideoPlayer(cutscene.VideoClip);
        VideoPlayer.Play();
    }

    private void ConfigureVideoPlayer(VideoClip clip)
    {
        VideoPlayer.playOnAwake = false;
        VideoPlayer.isLooping = false;
        VideoPlayer.source = VideoSource.VideoClip;
        VideoPlayer.clip = clip;

        if (VideoDisplay != null && VideoPlayer.targetTexture != null)
            VideoDisplay.texture = VideoPlayer.targetTexture;

        VideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        VideoPlayer.controlledAudioTrackCount = 1;
        VideoPlayer.EnableAudioTrack(0, true);
        VideoPlayer.SetDirectAudioMute(0, false);
        VideoPlayer.SetTargetAudioSource(0, CutsceneAudioSource);

        if (CutsceneAudioSource != null)
        {
            CutsceneAudioSource.playOnAwake = false;
            CutsceneAudioSource.mute = false;
        }
    }

    private SO_CutsceneVideo GetCutscene(E_CutsceneId cutsceneId)
    {
        SO_CutsceneVideo sceneLoaderCutscene = SceneLoader.Instance != null
            ? SceneLoader.Instance.GetPlayableCutscene(cutsceneId)
            : null;

        if (sceneLoaderCutscene != null)
            return sceneLoaderCutscene;

        if (Cutscenes == null) return null;

        for (int i = 0; i < Cutscenes.Length; i++)
        {
            if (Cutscenes[i] != null &&
                Cutscenes[i].CutsceneId == cutsceneId &&
                Cutscenes[i].VideoClip != null)
            {
                return Cutscenes[i];
            }
        }

        return null;
    }

    private void HandleVideoFinished(VideoPlayer source)
    {
        ShowCompletedControls();
    }

    private void ShowCompletedControls()
    {
        if (_hasFinished) return;

        _hasFinished = true;
        StopPlayback();

        if (IsPrologueCutscene())
        {
            OnContinueClicked();
            return;
        }

        SetControlsVisible(true);
    }

    private void SetControlsVisible(bool visible)
    {
        bool showSkip = !visible && Sc_CutsceneSession.HasActiveCutscene;
        bool showControlsPanel = visible || showSkip;

        if (ControlsPanel != null)
            ControlsPanel.SetActive(showControlsPanel);

        if (ContinueButton != null)
            ContinueButton.gameObject.SetActive(
                visible &&
                !IsPrologueCutscene() &&
                Sc_CutsceneSession.ContinueDestination != E_CutsceneDestination.None);

        if (BackToMainMenuButton != null)
            BackToMainMenuButton.gameObject.SetActive(visible && !IsPrologueCutscene());

        if (SkipButton != null)
            SkipButton.gameObject.SetActive(showSkip);
    }

    private void StopPlayback()
    {
        if (VideoPlayer != null && VideoPlayer.isPlaying)
            VideoPlayer.Stop();

        if (CutsceneAudioSource != null && CutsceneAudioSource.isPlaying)
            CutsceneAudioSource.Stop();
    }


    private void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
            return;
        }

        if (eventSystem.GetComponent<BaseInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }


    private void EnsureVideoDoesNotBlockControls()
    {
        if (VideoDisplay != null)
            VideoDisplay.raycastTarget = false;
    }


    private bool IsPrologueCutscene()
    {
        return Sc_CutsceneSession.ActiveCutsceneId == E_CutsceneId.Prologue;
    }


    private bool ShouldSkipFromKeyboard()
    {
        return !_hasFinished &&
            Sc_CutsceneSession.HasActiveCutscene &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    public void OnSkipClicked()
    {
        ShowCompletedControls();
    }

    public void OnContinueClicked()
    {
        Time.timeScale = 1f;
        SceneLoader.Instance.ContinueFromActiveCutscene();
    }

    public void OnBackToMainMenuClicked()
    {
        Time.timeScale = 1f;
        Sc_CutsceneSession.ClearAll();
        Sc_RunSession.Clear();
        SceneLoader.Instance.LoadMainMenu();
    }

}
