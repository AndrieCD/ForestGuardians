// Mb_EndScreenUI.cs
// Abstract base class for any full-screen end-of-game UI panel.
//
// WHY AN ABSTRACT BASE:
//   Mb_DefeatManager's RunEndSequence() holds a Screen reference of this type.
//   When the victory sequence is implemented, Mb_VictoryScreenUI simply inherits
//   from this class and overrides Show() with victory-specific copy and styling.
//   Mb_DefeatManager needs no changes to support victory — it just receives a
//   different Mb_EndScreenUI subclass through its config.
//
// Derived classes: Mb_DefeatScreenUI
// Future derived classes: Mb_VictoryScreenUI

using UnityEngine;

public abstract class Mb_EndScreenUI : MonoBehaviour
{
    /// <summary>
    /// Called by Mb_DefeatManager (or a future Mb_VictoryManager) after the
    /// end-of-game sequence finishes. Derived classes implement the fade-in
    /// and display the provided message string.
    /// </summary>
    /// <param name="message">The contextual message to display (selected upstream).</param>
    public abstract void Show(string message);
}