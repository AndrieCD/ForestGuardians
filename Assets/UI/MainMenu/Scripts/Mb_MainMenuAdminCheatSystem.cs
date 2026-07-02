using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hidden demo/admin command listener for the main menu.
/// Press Enter to begin typing a command, type the command, then press Enter again to execute it.
/// </summary>
public class Mb_MainMenuAdminCheatSystem : MonoBehaviour
{
    private const int MAX_COMMAND_LENGTH = 64;
    private const string LOAD_COMMAND_PREFIX = "load ";
    private const string TEST_STAGE_1_COMMAND = "TestStage1";
    private const string TEST_STAGE_2_COMMAND = "TestStage2";
    private const string FILL_ALMANAC_COMMAND = "fill almanac";
    private const string UNFILL_ALMANAC_COMMAND = "unfill almanac";

    [Header("Default Demo Selection")]
    [Tooltip("Guardian selected by demo load commands. Assign Rajah.")]
    [SerializeField] private SO_Guardian RajahGuardian;

    [Header("Command Rules")]
    [Tooltip("When enabled, commands only work while GameManager is in MainMenu state.")]
    [SerializeField] private bool RequireMainMenuState = true;

    [Tooltip("Writes command state and errors to the Unity Console.")]
    [SerializeField] private bool LogCommandFeedback = true;

    private bool _isCapturingCommand;
    private string _commandBuffer = string.Empty;

    private void OnEnable()
    {
        if (Keyboard.current != null)
            Keyboard.current.onTextInput += HandleTextInput;
    }

    private void OnDisable()
    {
        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= HandleTextInput;

        _isCapturingCommand = false;
        _commandBuffer = string.Empty;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (WasEnterPressed(keyboard))
        {
            HandleEnterPressed();
            return;
        }

        if (_isCapturingCommand && keyboard.backspaceKey.wasPressedThisFrame)
            RemoveLastCharacter();
    }

    private void HandleTextInput(char input)
    {
        if (!_isCapturingCommand) return;
        if (char.IsControl(input)) return;
        if (_commandBuffer.Length >= MAX_COMMAND_LENGTH) return;

        _commandBuffer += input;
    }

    private void HandleEnterPressed()
    {
        if (!CanUseCommands())
            return;

        if (!_isCapturingCommand)
        {
            BeginCommandCapture();
            return;
        }

        string command = _commandBuffer.Trim();
        _isCapturingCommand = false;
        _commandBuffer = string.Empty;

        ExecuteCommand(command);
    }

    private void BeginCommandCapture()
    {
        _isCapturingCommand = true;
        _commandBuffer = string.Empty;

        Log("Command input started.");
    }

    private void ExecuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            LogWarning("Command input ended with an empty command.");
            return;
        }

        if (TryGetTestStageSceneName(command, out string sceneName))
        {
            LoadDemoStage(sceneName);
            return;
        }

        if (TryExecuteAlmanacCommand(command))
            return;

        LogWarning($"Unknown command: {command}");
    }

    private bool TryExecuteAlmanacCommand(string command)
    {
        if (string.Equals(command, FILL_ALMANAC_COMMAND, System.StringComparison.OrdinalIgnoreCase))
        {
            FillAlmanac();
            return true;
        }

        if (string.Equals(command, UNFILL_ALMANAC_COMMAND, System.StringComparison.OrdinalIgnoreCase))
        {
            UnfillAlmanac();
            return true;
        }

        return false;
    }

    private bool TryGetTestStageSceneName(string command, out string sceneName)
    {
        sceneName = string.Empty;

        if (!command.StartsWith(LOAD_COMMAND_PREFIX, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string stageToken = command.Substring(LOAD_COMMAND_PREFIX.Length).Trim();

        if (string.Equals(stageToken, TEST_STAGE_1_COMMAND, System.StringComparison.OrdinalIgnoreCase))
        {
            sceneName = TEST_STAGE_1_COMMAND;
            return true;
        }

        if (string.Equals(stageToken, TEST_STAGE_2_COMMAND, System.StringComparison.OrdinalIgnoreCase))
        {
            sceneName = TEST_STAGE_2_COMMAND;
            return true;
        }

        return false;
    }

    private void LoadDemoStage(string sceneName)
    {
        if (RajahGuardian == null)
        {
            Debug.LogError("[Mb_MainMenuAdminCheatSystem] RajahGuardian is not assigned.");
            return;
        }

        if (SceneLoader.Instance == null)
        {
            Debug.LogError("[Mb_MainMenuAdminCheatSystem] SceneLoader.Instance is missing.");
            return;
        }

        Sc_RunSession.SelectedStageNumber = 0;
        Sc_RunSession.SelectedGuardian = RajahGuardian;

        Log($"Loading {sceneName} with {RajahGuardian.CharacterName}.");
        SceneLoader.Instance.LoadTestStage(sceneName);
    }

    private void FillAlmanac()
    {
        if (Mb_AlmanacManager.Instance == null)
        {
            Debug.LogError("[Mb_MainMenuAdminCheatSystem] Mb_AlmanacManager.Instance is missing.");
            return;
        }

        Mb_AlmanacManager.Instance.FillAllEntriesForDebug();
        Log("Almanac filled.");
    }

    private void UnfillAlmanac()
    {
        if (Mb_AlmanacManager.Instance == null)
        {
            Debug.LogError("[Mb_MainMenuAdminCheatSystem] Mb_AlmanacManager.Instance is missing.");
            return;
        }

        Mb_AlmanacManager.Instance.ClearAllEntriesForDebug();
        Log("Almanac cleared.");
    }

    private bool CanUseCommands()
    {
        if (!RequireMainMenuState) return true;

        if (GameManager.Instance == null)
        {
            LogWarning("GameManager.Instance is missing. Command ignored.");
            return false;
        }

        return GameManager.Instance.CurrentState == GameState.MainMenu;
    }

    private void RemoveLastCharacter()
    {
        if (_commandBuffer.Length == 0) return;

        _commandBuffer = _commandBuffer.Substring(0, _commandBuffer.Length - 1);
    }

    private bool WasEnterPressed(Keyboard keyboard)
    {
        return keyboard.enterKey.wasPressedThisFrame ||
               keyboard.numpadEnterKey.wasPressedThisFrame;
    }

    private void Log(string message)
    {
        if (!LogCommandFeedback) return;

        Debug.Log($"[Mb_MainMenuAdminCheatSystem] {message}");
    }

    private void LogWarning(string message)
    {
        if (!LogCommandFeedback) return;

        Debug.LogWarning($"[Mb_MainMenuAdminCheatSystem] {message}");
    }
}
