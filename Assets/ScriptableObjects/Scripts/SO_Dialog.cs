// SO_Dialog.cs
// A single dialog entry — one speaker, one text block, one voice clip.
//
// INSPECTOR SETUP:
//   - Portrait:    NPC portrait sprite. Leave null for narrator/system lines (no icon shown).
//   - SpeakerName: Displayed above the dialog text. Leave empty for narrator lines.
//   - DialogText:  The full line of text. Displayed all at once — no typewriter.
//   - VoiceClip:   Plays when this dialog becomes active. Its duration drives auto-dismiss.
//                  If null, the dialog dismisses after a fallback duration (set on Mb_DialogManager).
//   - IsTutorialInstruction: If true, this dialog stays open even after the audio ends.
//                  It waits for CompleteInstruction(CompletionEventKey) to be called.
//   - CompletionEventKey: The string key that closes this dialog when matched.
//                  Use constants from Sc_DialogEvents — never raw strings.
//                  Only relevant when IsTutorialInstruction is true.

using UnityEngine;

[CreateAssetMenu(fileName = "New Dialog", menuName = "Forest Guardians/Dialog/Dialog")]
public class SO_Dialog : ScriptableObject
{
    [Header("Speaker")]
    [Tooltip("NPC portrait icon. Null = no portrait shown (narrator or system voice).")]
    public Sprite Portrait;

    [Tooltip("Name shown above the dialog text. Empty = no name label shown.")]
    public string SpeakerName;

    [Header("Content")]
    [Tooltip("The full line of dialog. Displayed all at once.")]
    [TextArea(3, 6)]
    public string DialogText;

    [Tooltip("Voice clip that plays when this dialog becomes active. " +
             "Auto-dismiss fires when the clip ends. " +
             "If null, Mb_DialogManager uses its FallbackDismissDuration instead.")]
    public AudioClip VoiceClip;

    [Header("Tutorial Instruction")]
    [Tooltip("If true, this dialog stays open after audio ends until CompleteInstruction() " +
             "is called with the matching CompletionEventKey.")]
    public bool IsTutorialInstruction;

    [Tooltip("The event key that closes this dialog when matched. " +
             "Use constants from Sc_DialogEvents. Only used when IsTutorialInstruction is true.")]
    public string CompletionEventKey;
}