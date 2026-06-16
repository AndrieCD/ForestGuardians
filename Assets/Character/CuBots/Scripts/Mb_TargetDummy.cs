// Mb_TargetDummy.cs
// A stationary CuBot used for testing damage, crits, augments, and ability effects.
//
// BEHAVIOR:
//   - Takes damage normally from all sources (projectiles, melee AoE, etc.)
//   - When killed, fires OnCuBotDeath and OnCuBotKill just like any real CuBot —
//     so Royal Plumage stacks, Cycle of Life kills, and other kill-reactive systems
//     all work correctly during testing.
//   - Immediately revives itself after death by re-enabling the GameObject
//     one frame after deactivation. Health is fully restored on revival.
//   - Never moves. Never attacks.
//   - OneLifeOnly: if true, the dummy does NOT revive after death.
//     Used in tutorial waves where a kill must register for wave completion.
//
// Inspector setup:
//   - Assign an SO_CuBots template (any will do — only stats are read, no abilities)
//   - Add to scene directly; no object pool needed for a test dummy
//   - Requires: Mb_StatBlock, Mb_HealthComponent, Mb_AbilityController on the same GO
//   - Does NOT require: Mb_Movement, NavMeshAgent, Animator

using UnityEngine;

public class Mb_TargetDummy : MB_CuBotBase
{
    [Header("Target Dummy Settings")]
    [Tooltip("How long the dummy stays dead before reviving. Set to 0 for instant revival.")]
    [SerializeField] private float ReviveDelay = 0.5f;

    [Tooltip("If true, the dummy dies permanently and does not revive. " +
             "Use this for tutorial waves where wave completion requires a kill.")]
    [SerializeField] private bool OneLifeOnly = false;


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void OnDisable()
    {
        // If this is a one-life dummy, do not schedule a revive.
        // The dummy stays dead — MB_CuBotBase fires OnCuBotDeath normally,
        // which satisfies Mb_WaveManager's active enemy count.
        if (OneLifeOnly) return;

        // When the base class calls gameObject.SetActive(false) on death,
        // this fires immediately. We schedule a re-enable after ReviveDelay
        // so the dummy pops back to life automatically.
        //
        // CancelInvoke guards against edge cases where OnDisable fires
        // more than once before the Invoke executes (e.g. rapid kills).
        CancelInvoke(nameof(Revive));
        Invoke(nameof(Revive), ReviveDelay);
    }


    // -------------------------------------------------------------------------
    // Revive
    // -------------------------------------------------------------------------

    private void Revive()
    {
        // Re-enabling triggers OnEnable → Reset() → InitializeFromTemplate()
        // in MB_CuBotBase, which restores full health and clears all modifiers.
        // Nothing else is needed here.
        gameObject.SetActive(true);
        Debug.Log($"[Target Dummy] Revived after {ReviveDelay}s.");
    }


    // -------------------------------------------------------------------------
    // No Abilities
    // -------------------------------------------------------------------------

    protected override void AssignAbilities()
    {
        // Target dummies don't attack — intentionally empty.
        // Mb_AbilityController is still required on the GameObject so
        // MB_CuBotBase doesn't throw a null ref, but no slots are filled.
    }
}