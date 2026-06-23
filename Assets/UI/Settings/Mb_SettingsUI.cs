// Mb_SettingsUI.cs
// Drives the Settings panel canvas — tabs, sliders, toggles, and buttons.
//
// PANEL STRUCTURE (build this in the Unity Editor):
//
//   SettingsCanvas (this GameObject — starts INACTIVE)
//   ├── SettingsContainer
//   │   ├── TabBar
//   │   │   ├── Tab_Audio    (Button)
//   │   │   ├── Tab_Display  (Button)
//   │   │   └── Tab_Controls (Button)
//   │   ├── Panel_Audio
//   │   │   ├── MasterVolumeSlider  (Slider)
//   │   │   ├── MusicVolumeSlider   (Slider)
//   │   │   └── SFXVolumeSlider     (Slider)
//   │   ├── Panel_Display
//   │   │   ├── FullscreenToggle    (Toggle  — or a custom two-state button)
//   │   │   └── QualityGroup
//   │   │       ├── Btn_Low         (Button)
//   │   │       ├── Btn_Medium      (Button)
//   │   │       └── Btn_High        (Button)
//   │   ├── Panel_Controls          (static display only)
//   │   │   ├── ColumnLeft          (TMP_Text)
//   │   │   └── ColumnRight         (TMP_Text)
//   │   └── FooterButtons
//   │       ├── Btn_Back    (Button)
//   │       ├── Btn_Save    (Button)
//   │       └── Btn_Revert  (Button)
//
// INSPECTOR SETUP:
//   - Wire all [SerializeField] references in the Inspector.
//   - Wire each Button.OnClick() to the matching public method on this component.
//   - QualityButtonColors: set Selected and Deselected colors in the Inspector
//     so the active quality button is visually distinct.
//   - The Controls panel text is populated from the ControlsLeft/Right arrays
//     in the Inspector — no code changes needed when bindings change.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mb_SettingsUI : MonoBehaviour
{
    // ── Tab Panels ────────────────────────────────────────────────────────

    [Header("Tab Panels")]
    [SerializeField] private GameObject Panel_Audio;
    [SerializeField] private GameObject Panel_Display;
    [SerializeField] private GameObject Panel_Controls;

    [Header("Tab Buttons")]
    [SerializeField] private Button Tab_Audio;
    [SerializeField] private Button Tab_Display;
    [SerializeField] private Button Tab_Controls;

    [Header("Tab Active Colors")]
    [Tooltip("Color applied to the tab button that is currently selected.")]
    [SerializeField] private Color TabActiveColor = new Color(0.96f, 0.80f, 0.40f); // warm gold
    [Tooltip("Color applied to inactive tab buttons.")]
    [SerializeField] private Color TabInactiveColor = new Color(0.78f, 0.65f, 0.45f); // muted tan

    // ── Audio ─────────────────────────────────────────────────────────────

    [Header("Audio Sliders")]
    [SerializeField] private Slider MasterVolumeSlider;
    [SerializeField] private Slider MusicVolumeSlider;
    [SerializeField] private Slider SFXVolumeSlider;

    // ── Display ───────────────────────────────────────────────────────────

    [Header("Display — Fullscreen")]
    [Tooltip("Toggle that switches between Windowed (off) and Fullscreen (on).")]
    [SerializeField] private Toggle FullscreenToggle;

    [Header("Display — Quality Buttons")]
    [SerializeField] private Button Btn_Quality_Low;
    [SerializeField] private Button Btn_Quality_Medium;
    [SerializeField] private Button Btn_Quality_High;

    [Header("Quality Button Colors")]
    [SerializeField] private Color QualitySelectedColor = new Color(0.96f, 0.80f, 0.40f);
    [SerializeField] private Color QualityDeselectedColor = new Color(0.78f, 0.65f, 0.45f);

    // ── Controls (static display) ─────────────────────────────────────────

    [Header("Controls Panel")]
    [SerializeField] private TMP_Text ControlsLeftText;
    [SerializeField] private TMP_Text ControlsRightText;

    [Tooltip("Action names shown in the left column. One entry per row.")]
    [SerializeField]
    private string[] ControlsLeft = new string[]
    {
        "Move",
        "Jump",
        "Dash",
        "Primary Attack",
        "Secondary Attack",
        "Ability Q",
        "Ability E",
        "Ability R",
        "Pause",
    };

    [Tooltip("Key bindings shown in the right column. Must match ControlsLeft length.")]
    [SerializeField]
    private string[] ControlsRight = new string[]
    {
        "W A S D",
        "Space",
        "Left Shift",
        "LMB",
        "RMB",
        "Q",
        "E",
        "R",
        "Esc",
    };

    // ── Private State ─────────────────────────────────────────────────────

    // Tracks which quality button is currently selected (0=Low, 1=Med, 2=High)
    private int _selectedQuality = 1;

    // Prevents slider callbacks from firing ApplyAudio() during Initialize()
    // when we set slider values programmatically
    private bool _isInitializing = false;

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        // Wire slider callbacks
        MasterVolumeSlider.onValueChanged.AddListener(_ => OnAudioSliderChanged());
        MusicVolumeSlider.onValueChanged.AddListener(_ => OnAudioSliderChanged());
        SFXVolumeSlider.onValueChanged.AddListener(_ => OnAudioSliderChanged());

        // Wire fullscreen toggle callback
        FullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
    }

    private void OnEnable()
    {
        //GameManager.Instance.OnGameStateChanged += OnGameStateChanged;

        Mb_PauseManager.OnResumed += () => gameObject?.SetActive(false);

        // Refresh controls from the current saved/working state every time the panel opens
        Initialize();
    }

    private void OnGameStateChanged(GameState obj)
    {
        if (gameObject == null) return;
        if (obj != GameState.Paused)
        {
            // Close settings if we leave the Paused state — prevents weird edge cases
            // where settings are left open while playing, or open to the wrong state
            // when pausing from a different state.
            gameObject.SetActive(false);
        }
    }

    // ── Initialization ────────────────────────────────────────────────────

    private void Initialize()
    {
        if (Mb_SettingsManager.Instance == null)
        {
            Debug.LogError("[Mb_SettingsUI] Mb_SettingsManager not found.");
            return;
        }

        _isInitializing = true;

        Sc_SettingsData current = Mb_SettingsManager.Instance.Current;

        // Audio sliders — range 0–1
        MasterVolumeSlider.value = current.MasterVolume;
        MusicVolumeSlider.value = current.MusicVolume;
        SFXVolumeSlider.value = current.SFXVolume;

        // Display
        FullscreenToggle.isOn = current.IsFullscreen;
        _selectedQuality = current.GraphicsQuality;
        RefreshQualityButtons();

        // Controls static text
        BuildControlsText();

        // Default to Audio tab on open
        ShowTab(0);

        _isInitializing = false;
    }

    // ── Tab Navigation ────────────────────────────────────────────────────

    // Called by Tab_Audio Button.OnClick()
    public void OnTabAudioClicked() => ShowTab(0);

    // Called by Tab_Display Button.OnClick()
    public void OnTabDisplayClicked() => ShowTab(1);

    // Called by Tab_Controls Button.OnClick()
    public void OnTabControlsClicked() => ShowTab(2);

    private void ShowTab(int index)
    {
        Panel_Audio.SetActive(index == 0);
        Panel_Display.SetActive(index == 1);
        Panel_Controls.SetActive(index == 2);

        SetTabColor(Tab_Audio, index == 0);
        SetTabColor(Tab_Display, index == 1);
        SetTabColor(Tab_Controls, index == 2);
    }

    private void SetTabColor(Button tab, bool active)
    {
        if (tab == null) return;

        // Tint the button's target graphic directly — avoids fighting with
        // the ColorBlock system which resets on highlight/press
        Image img = tab.GetComponent<Image>();
        if (img != null)
            img.color = active ? TabActiveColor : TabInactiveColor;
    }

    // ── Audio Callbacks ───────────────────────────────────────────────────

    private void OnAudioSliderChanged()
    {
        if (_isInitializing) return;

        Mb_SettingsManager.Instance.ApplyAudio(
            MasterVolumeSlider.value,
            MusicVolumeSlider.value,
            SFXVolumeSlider.value
        );
    }

    // ── Display Callbacks ─────────────────────────────────────────────────

    private void OnFullscreenToggleChanged(bool isOn)
    {
        if (_isInitializing) return;

        Mb_SettingsManager.Instance.ApplyDisplay(isOn, _selectedQuality);
    }

    // Called by Btn_Quality_Low.OnClick()
    public void OnQualityLowClicked()
    {
        _selectedQuality = 0;
        RefreshQualityButtons();
        Mb_SettingsManager.Instance.ApplyDisplay(FullscreenToggle.isOn, _selectedQuality);
    }

    // Called by Btn_Quality_Medium.OnClick()
    public void OnQualityMediumClicked()
    {
        _selectedQuality = 1;
        RefreshQualityButtons();
        Mb_SettingsManager.Instance.ApplyDisplay(FullscreenToggle.isOn, _selectedQuality);
    }

    // Called by Btn_Quality_High.OnClick()
    public void OnQualityHighClicked()
    {
        _selectedQuality = 2;
        RefreshQualityButtons();
        Mb_SettingsManager.Instance.ApplyDisplay(FullscreenToggle.isOn, _selectedQuality);
    }

    private void RefreshQualityButtons()
    {
        SetQualityButtonColor(Btn_Quality_Low, _selectedQuality == 0);
        SetQualityButtonColor(Btn_Quality_Medium, _selectedQuality == 1);
        SetQualityButtonColor(Btn_Quality_High, _selectedQuality == 2);
    }

    private void SetQualityButtonColor(Button btn, bool selected)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = selected ? QualitySelectedColor : QualityDeselectedColor;
    }

    // ── Footer Buttons ────────────────────────────────────────────────────

    // Called by Btn_Save.OnClick()
    public void OnSaveClicked()
    {
        Mb_SettingsManager.Instance.Save();
    }

    // Called by Btn_Revert.OnClick()
    public void OnRevertClicked()
    {
        Mb_SettingsManager.Instance.Revert();

        // Re-initialize UI to show reverted values
        _isInitializing = true;

        Sc_SettingsData current = Mb_SettingsManager.Instance.Current;
        MasterVolumeSlider.value = current.MasterVolume;
        MusicVolumeSlider.value = current.MusicVolume;
        SFXVolumeSlider.value = current.SFXVolume;
        FullscreenToggle.isOn = current.IsFullscreen;
        _selectedQuality = current.GraphicsQuality;
        RefreshQualityButtons();

        _isInitializing = false;
    }

    // Called by Btn_Back.OnClick()
    // Closes without saving — working copy changes are discarded on next open
    // because Initialize() always pulls from Mb_SettingsManager.Current,
    // which tracks the working copy. If you want Back to also revert,
    // call Mb_SettingsManager.Instance.Revert() here before hiding.
    public void OnBackClicked()
    {
        // Optional: revert unsaved changes when closing with Back
        Mb_SettingsManager.Instance.Revert();

        gameObject.SetActive(false);
    }

    // ── Controls Text Builder ─────────────────────────────────────────────

    private void BuildControlsText()
    {
        if (ControlsLeftText == null || ControlsRightText == null) return;

        System.Text.StringBuilder left = new System.Text.StringBuilder();
        System.Text.StringBuilder right = new System.Text.StringBuilder();

        int rowCount = Mathf.Min(ControlsLeft.Length, ControlsRight.Length);

        for (int i = 0; i < rowCount; i++)
        {
            left.AppendLine(ControlsLeft[i]);
            right.AppendLine(ControlsRight[i]);
        }

        ControlsLeftText.text = left.ToString().TrimEnd();
        ControlsRightText.text = right.ToString().TrimEnd();
    }
}