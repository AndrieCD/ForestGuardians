
using System;
using System.Collections;
using UnityEngine;

[Serializable]
public struct Sc_EndSequenceConfig
{
    // Messages to choose from randomly when this sequence triggers.
    // Populate in the Inspector or via code in the relevant Setup method.
    public string[] Messages;

    // The screen to show after the sequence completes.
    // Assign in Inspector.
    public Mb_EndScreenUI Screen;

    // How much to slow time during the dramatic hold (0.5 = half speed, 1.0 = no change).
    // Defeat uses 0.5. Victory could use 1.0 or a different value.
    public float TimeScaleMultiplier;

    // The GameState to transition into when the sequence begins.
    // Defeat → GameState.Defeat. Victory → GameState.Victory.
    public GameState TargetGameState;
}