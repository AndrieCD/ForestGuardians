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
//
// Inspector setup:
//   - Assign an SO_CuBots template (any will do — only stats are read, no abilities)
//   - Add to scene directly; no object pool needed for a test dummy
//   - Requires: Mb_StatBlock, Mb_HealthComponent, Mb_AbilityController on the same GO
//   - Does NOT require: Mb_Movement, NavMeshAgent, Animator

using UnityEngine;

public class Mb_TargetDummy : MB_CuBotBase
{
    // How long (in seconds) the dummy stays "dead" before reviving.
    // A small delay makes the revival visible in the scene — good for testing death VFX.
    // Set to 0f if you want instant revival with no visible downtime.
    [Header("Target Dummy Settings")]
    [SerializeField] private float ReviveDelay = 0.5f;


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void OnDisable()
    {
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