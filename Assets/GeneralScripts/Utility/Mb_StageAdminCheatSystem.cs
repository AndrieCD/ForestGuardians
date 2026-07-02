using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hidden admin command listener for active stage gameplay.
/// Press Enter to begin typing a command, type the command, then press Enter again to execute it.
/// </summary>
public class Mb_StageAdminCheatSystem : MonoBehaviour
{
    private const int MAX_COMMAND_LENGTH = 64;
    private const string GODMODE_COMMAND = "godmode";

    [Header("Command Rules")]
    [Tooltip("When enabled, commands only work while GameManager is in Playing state.")]
    [SerializeField] private bool RequirePlayingState = true;

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

        Log("Stage command input started.");
    }

    private void ExecuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            LogWarning("Stage command input ended with an empty command.");
            return;
        }

        if (string.Equals(command, GODMODE_COMMAND, System.StringComparison.OrdinalIgnoreCase))
        {
            EnableGodMode();
            return;
        }

        LogWarning($"Unknown stage command: {command}");
    }

    private void EnableGodMode()
    {
        Mb_HealthComponent.SetPlayerGodModeEnabled(true);
        Log("God mode enabled.");
    }

    private bool CanUseCommands()
    {
        if (!RequirePlayingState) return true;

        if (GameManager.Instance == null)
        {
            LogWarning("GameManager.Instance is missing. Stage command ignored.");
            return false;
        }

        return GameManager.Instance.CurrentState == GameState.Playing;
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

        Debug.Log($"[Mb_StageAdminCheatSystem] {message}");
    }

    private void LogWarning(string message)
    {
        if (!LogCommandFeedback) return;

        Debug.LogWarning($"[Mb_StageAdminCheatSystem] {message}");
    }
}
