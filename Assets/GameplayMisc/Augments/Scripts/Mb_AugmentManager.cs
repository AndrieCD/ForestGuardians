// Mb_AugmentManager.cs
// Lives on the Stage GameObject alongside Mb_WaveManager and Mb_RewardsManager.
//
// This is the single source of truth for "what augments does the player currently have."
// It holds every equipped augment for the entire stage run.
//
// RESPONSIBILITIES:
//   - Store the list of active augment instances
//   - Enforce the 3-augment cap
//   - Call OnEquip when a new augment is added
//   - Call OnWaveEnd on all augments at the end of every wave
//   - Call OnUnequip on all augments and clear the list at stage end
//
// Inspector setup: assign the Player GameObject reference in the Inspector.
// Mb_RewardsManager (a sibling on this same GameObject) calls AddAugment().

using System.Collections.Generic;
using UnityEngine;

public class Mb_AugmentManager : MonoBehaviour
{
    [Header("References")]
    // Drag the Player GameObject here in the Inspector.
    // We need it to reach Mb_CharacterBase for OnEquip/OnUnequip calls.
    [SerializeField] private GameObject _PlayerObject;

    // The max number of augments the player can hold in one stage
    private const int MAX_AUGMENTS = 3;

    // Every augment the player has picked up this stage, in selection order
    private List<Sc_AugmentBase> _equippedAugments = new List<Sc_AugmentBase>();

    // Cached reference to the player's character component — fetched once in Start
    private Mb_CharacterBase _player;


    private void Start()
    {
        // Cache the player character reference once so we don't search every time
        if (_PlayerObject != null)
            _player = _PlayerObject.GetComponent<Mb_CharacterBase>();

        if (_player == null)
            Debug.LogError("[Mb_AugmentManager] Could not find Mb_CharacterBase on the assigned Player object.");
    }


    private void OnEnable()
    {
        // Listen to wave end so we can tick all augments that need per-wave resets
        Mb_WaveManager.OnWaveEnd += HandleWaveEnd;

        // Listen to stage end so we clean everything up
        Mb_StageManager.OnStageEnd += HandleStageEnd;
    }


    private void OnDisable()
    {
        Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;
        Mb_StageManager.OnStageEnd -= HandleStageEnd;
    }


    // -------------------------------------------------------------------------
    // Public API — called by Mb_RewardsManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if the player can still receive another augment this stage.
    /// Mb_RewardsManager checks this before offering augment rewards.
    /// </summary>
    public bool CanEquipMore => _equippedAugments.Count < MAX_AUGMENTS;


    /// <summary>
    /// Equips one augment on the player. Called by Mb_RewardsManager when the
    /// player makes their selection on the rewards panel.
    /// </summary>
    public void AddAugment(Sc_AugmentBase augment)
    {
        if (!CanEquipMore)
        {
            // This shouldn't happen if Mb_RewardsManager checks CanEquipMore first,
            // but we guard here anyway as a safety net.
            Debug.LogWarning("[Mb_AugmentManager] Tried to add augment but cap of 3 is already reached.");
            return;
        }

        _equippedAugments.Add(augment);

        // Apply the augment's stat effects immediately
        augment.OnEquip(_player);

        Debug.Log($"[Mb_AugmentManager] Equipped augment: {augment.AugmentName} ({_equippedAugments.Count}/{MAX_AUGMENTS})");
    }


    /// <summary>
    /// Returns a read-only snapshot of the currently equipped augments.
    /// Used by UI or debug tools that need to display active augments.
    /// </summary>
    public IReadOnlyList<Sc_AugmentBase> GetEquippedAugments()
    {
        return _equippedAugments.AsReadOnly();
    }


    // -------------------------------------------------------------------------
    // Event Handlers
    // -------------------------------------------------------------------------

    private void HandleWaveEnd(int completedWaveIndex)
    {
        // Give every equipped augment a chance to do per-wave logic.
        // Most augments have an empty OnWaveEnd and this costs essentially nothing.
        foreach (var augment in _equippedAugments)
        {
            augment.OnWaveEnd();
        }
    }


    private void HandleStageEnd()
    {
        // Unequip every augment in reverse order — good practice when
        // augments might have dependencies on each other's effects
        for (int i = _equippedAugments.Count - 1; i >= 0; i--)
        {
            _equippedAugments[i].OnUnequip(_player);
        }

        _equippedAugments.Clear();

        Debug.Log("[Mb_AugmentManager] All augments cleared at stage end.");
    }
}