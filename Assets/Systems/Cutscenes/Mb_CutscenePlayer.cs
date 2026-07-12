using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Scene Routing")]
    [SerializeField] private string CreditsSceneName = "Credits";

    private bool _hasFinished;

    private void Awake()
    {
        if (VideoPlayer == null)
            Debug.LogError("[Mb_CutscenePlayer] VideoPlayer is not assigned.");

        if (CutsceneAudioSource == null)
            Debug.LogError("[Mb_CutscenePlayer] CutsceneAudioSource is not assigned.");
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
            ShowCompletedControls();
            return;
        }

        SO_CutsceneVideo cutscene = GetCutscene(Sc_CutsceneSession.ActiveCutsceneId);
        if (cutscene == null || cutscene.VideoClip == null)
        {
            Debug.LogError($"[Mb_CutscenePlayer] Missing video clip for cutscene " +
                           $"{Sc_CutsceneSession.ActiveCutsceneId}.");
            ShowCompletedControls();
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
        if (Cutscenes == null) return null;

        for (int i = 0; i < Cutscenes.Length; i++)
        {
            if (Cutscenes[i] != null && Cutscenes[i].CutsceneId == cutsceneId)
                return Cutscenes[i];
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

        E_CutsceneDestination destination = Sc_CutsceneSession.ContinueDestination;
        int stageNumber = Sc_CutsceneSession.ContinueStageNumber;
        string sceneName = Sc_CutsceneSession.ContinueSceneName;

        Sc_CutsceneSession.ClearActiveCutscene();

        switch (destination)
        {
            case E_CutsceneDestination.MainMenu:
                Sc_RunSession.Clear();
                SceneLoader.Instance.LoadMainMenu();
                break;

            case E_CutsceneDestination.Stage:
                LoadStageAfterCutscene(stageNumber);
                break;

            case E_CutsceneDestination.Credits:
                SceneLoader.Instance.LoadCredits(string.IsNullOrWhiteSpace(sceneName) ? CreditsSceneName : sceneName);
                break;

            default:
                Debug.LogWarning("[Mb_CutscenePlayer] Continue clicked with no valid destination.");
                break;
        }
    }

    public void OnBackToMainMenuClicked()
    {
        Time.timeScale = 1f;
        Sc_CutsceneSession.ClearAll();
        Sc_RunSession.Clear();
        SceneLoader.Instance.LoadMainMenu();
    }

    private void LoadStageAfterCutscene(int stageNumber)
    {
        if (stageNumber <= 0)
            stageNumber = Sc_RunSession.SelectedStageNumber;

        Sc_RunSession.SelectedStageNumber = stageNumber;

        if (!Sc_RunSession.IsValid())
        {
            Debug.LogError("[Mb_CutscenePlayer] Cannot continue to stage because Sc_RunSession is invalid.");
            SceneLoader.Instance.LoadMainMenu();
            return;
        }

        if (stageNumber == Sc_RunSession.TUTORIAL_STAGE)
        {
            SceneLoader.Instance.LoadTutorial();
            return;
        }

        SceneLoader.Instance.LoadStage(stageNumber);
    }
}
