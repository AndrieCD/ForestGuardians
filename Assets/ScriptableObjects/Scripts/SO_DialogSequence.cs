// SO_DialogSequence.cs
// An ordered list of SO_Dialog entries played one after another.
// This is the primary unit other systems pass to Mb_DialogManager —
// wave SOs, stage SOs, and tutorial SOs all reference a sequence,
// never individual dialogs directly.
//
// INSPECTOR SETUP:
//   - Dialogs: Add SO_Dialog assets in the order they should play.
//     Mb_DialogManager will enqueue them left-to-right (index 0 first).

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Dialog Sequence", menuName = "Forest Guardians/Dialog/Dialog Sequence")]
public class SO_DialogSequence : ScriptableObject
{
    [Tooltip("Dialogs played in order, top to bottom. " +
             "Mb_DialogManager enqueues them all when EnqueueSequence() is called.")]
    public List<SO_Dialog> Dialogs = new List<SO_Dialog>();
}