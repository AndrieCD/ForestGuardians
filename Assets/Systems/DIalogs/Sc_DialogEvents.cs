// Sc_DialogEvents.cs
// Static string constants for all valid dialog completion event keys.
//
// WHY THIS EXISTS:
//   Tutorial dialogs close when Mb_DialogManager.CompleteInstruction(key) is called
//   with a key that matches the active dialog's CompletionEventKey field.
//   Without a central constants class, those keys would be raw strings scattered
//   across the codebase — a typo anywhere silently breaks a tutorial step.
//
// USAGE:
//   - Assign constants from this class to SO_Dialog.CompletionEventKey in the Inspector.
//   - Call Mb_DialogManager.Instance.CompleteInstruction(Sc_DialogEvents.PlayerUsedQ)
//     from whatever script detects the relevant event (Mb_TutorialManager, etc.).
//
// EXTENDING:
//   Add a new const string here whenever a new tutorial task is designed.
//   The string value is the internal key — it never appears in UI.

public static class Sc_DialogEvents
{
    // --- Ability usage ---
    public const string PlayerUsedQ = "player_used_q";
    public const string PlayerUsedE = "player_used_e";
    public const string PlayerUsedR = "player_used_r";
    public const string PlayerUsedPrimary = "player_used_primary";
    public const string PlayerUsedSecondary = "player_used_secondary";

    // --- Combat ---
    public const string EnemyKilled = "enemy_killed";
    public const string DummyHit = "dummy_hit";

    // --- Movement ---
    public const string PlayerJumped = "player_jumped";
    public const string PlayerDashed = "player_dashed";

    // --- Stage flow ---
    public const string WaveCleared = "wave_cleared";
    public const string RewardChosen = "reward_chosen";

    // TODO: Add more keys here as tutorial tasks expand.
    // Keep keys lowercase with underscores — never change an existing key value
    // once it has been assigned to a SO_Dialog asset, or that asset will silently break.
}