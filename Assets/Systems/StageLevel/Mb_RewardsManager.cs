// Mb_RewardsManager.cs
// Lives on the Stage GameObject alongside Mb_WaveManager and Mb_AugmentManager.
//
// RESPONSIBILITIES:
//   - Listen to OnWaveEnd and check if this wave has a reward
//   - Determine the reward type (Augment, Ability Upgrade, Ultimate Branch)
//   - Select two options from the appropriate pool (never repeating augments already seen)
//   - Open the Rewards Panel UI and pass it the two options
//   - Receive the player's choice and dispatch it to the right system
//   - Resume the game after selection
//
// Inspector setup:
//   - Assign the full SO_Augment pool list in the Inspector
//   - Assign the RewardsPanel UI GameObject
//   - Mb_AugmentManager is fetched automatically via GetComponent (same GameObject)
//   - Mb_AbilityController is fetched from the Player tag

using System.Collections.Generic;
using UnityEngine;

public class Mb_RewardsManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector References
    // -------------------------------------------------------------------------

    [Header("Augment Pool")]
    // Drag all SO_Augment assets into this list in the Inspector.
    // This is the full pool of augments that can be offered this stage.
    [SerializeField] private List<SO_Augment> _AugmentPool;

    [Header("UI")]
    // Drag the Rewards Panel Canvas or root GameObject here.
    [SerializeField] private Mb_RewardsPanelUI _RewardsPanelUI;

    [Header("References")]
    [SerializeField] private GameObject _PlayerObject;


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    // Sibling component on the same Stage GameObject
    private Mb_AugmentManager _augmentManager;

    // Player's ability controller — needed for ability upgrade rewards
    private Mb_AbilityController _abilityController;

    // Tracks which SO_Augment assets have already been offered this stage,
    // so the same augment is never offered twice
    private HashSet<SO_Augment> _offeredAugments = new HashSet<SO_Augment>();

    // The two options currently displayed — held here so we know what the player picked
    private RewardOption _leftOption;
    private RewardOption _rightOption;


    // -------------------------------------------------------------------------
    // Wave Reward Schedule
    // Defines what reward type (if any) follows each wave.
    // Key = wave index (0-based), Value = reward type.
    // Waves with no reward simply aren't in the dictionary.
    // -------------------------------------------------------------------------
    private static readonly Dictionary<int, RewardType> REWARD_SCHEDULE = new Dictionary<int, RewardType>
    {
        { 0,  RewardType.AbilityUpgrade },      // After Wave 1
        { 1,  RewardType.AbilityUpgrade },      // After Wave 2
        { 3,  RewardType.AugmentSelection },    // After Wave 4
        { 4,  RewardType.UltimateBranch },      // After Wave 5
        { 5,  RewardType.AbilityUpgrade },      // After Wave 6
        { 6,  RewardType.AbilityUpgrade },      // After Wave 7
        { 8,  RewardType.AugmentSelection },    // After Wave 9
        { 9,  RewardType.AbilityUpgrade },      // After Wave 10
        { 10, RewardType.AbilityUpgrade },      // After Wave 11
        { 11, RewardType.AbilityUpgrade },      // After Wave 12
        { 12, RewardType.AugmentSelection },    // After Wave 13
    };


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // Fetch sibling AugmentManager — it lives on the same Stage GameObject
        _augmentManager = GetComponent<Mb_AugmentManager>();
        if (_augmentManager == null)
            Debug.LogError("[Mb_RewardsManager] Missing Mb_AugmentManager on this GameObject.");

        // Fetch the player's AbilityController for ability upgrade rewards
        if (_PlayerObject != null)
            _abilityController = _PlayerObject.GetComponent<Mb_AbilityController>();

        if (_abilityController == null)
            Debug.LogError("[Mb_RewardsManager] Could not find Mb_AbilityController on the Player object.");
    }


    private void OnEnable()
    {
        Mb_WaveManager.OnWaveEnd += HandleWaveEnd;
    }


    private void OnDisable()
    {
        Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;
    }


    // -------------------------------------------------------------------------
    // Wave End Handler
    // -------------------------------------------------------------------------

    private void HandleWaveEnd(int completedWaveIndex)
    {
        // Check if this wave has a reward scheduled
        if (!REWARD_SCHEDULE.TryGetValue(completedWaveIndex, out RewardType rewardType))
        {
            // No reward this wave — wave resolution continues normally
            return;
        }

        // Augment selection: skip if player already has 3 augments
        if (rewardType == RewardType.AugmentSelection && !_augmentManager.CanEquipMore)
        {
            Debug.Log("[Mb_RewardsManager] Augment reward skipped — player has max augments.");
            return;
        }

        OpenRewardsPanel(rewardType);
    }


    private void OpenRewardsPanel(RewardType rewardType)
    {
        // Build the two options based on what type of reward this is
        bool optionsReady = TryBuildOptions(rewardType, out _leftOption, out _rightOption);

        if (!optionsReady)
        {
            // Edge case: pool is exhausted or branch already selected — skip gracefully
            Debug.LogWarning($"[Mb_RewardsManager] Could not build options for {rewardType}. Skipping reward.");
            return;
        }

        // Pause game flow and hand control to the UI
        GameManager.Instance.ChangeState(GameState.RewardsPanel);
        _RewardsPanelUI.Show(_leftOption, _rightOption);
    }


    // -------------------------------------------------------------------------
    // Option Building
    // -------------------------------------------------------------------------

    private bool TryBuildOptions(RewardType rewardType, out RewardOption left, out RewardOption right)
    {
        left = default;
        right = default;

        if (rewardType == RewardType.AugmentSelection)
            return TryBuildAugmentOptions(out left, out right);

        // TODO: Add TryBuildAbilityUpgradeOptions() and TryBuildUltimateBranchOptions()
        // when those systems are implemented
        Debug.LogWarning($"[Mb_RewardsManager] Reward type {rewardType} not yet implemented.");
        return false;
    }


    private bool TryBuildAugmentOptions(out RewardOption left, out RewardOption right)
    {
        left = default;
        right = default;

        // Build a pool of augments that haven't been offered yet this stage
        List<SO_Augment> available = new List<SO_Augment>();
        foreach (var augment in _AugmentPool)
        {
            if (!_offeredAugments.Contains(augment))
                available.Add(augment);
        }

        // Need at least two options to show the panel
        if (available.Count < 2)
        {
            Debug.LogWarning("[Mb_RewardsManager] Not enough un-offered augments to fill the panel.");
            return false;
        }

        // Pick two at random without replacement
        int leftIndex = Random.Range(0, available.Count);
        SO_Augment leftData = available[leftIndex];
        available.RemoveAt(leftIndex);
        SO_Augment rightData = available[Random.Range(0, available.Count)];

        // Mark both as offered so they won't appear again this stage
        _offeredAugments.Add(leftData);
        _offeredAugments.Add(rightData);

        // Wrap into RewardOption structs for the UI to consume
        left = RewardOption.FromAugment(leftData);
        right = RewardOption.FromAugment(rightData);

        return true;
    }


    // -------------------------------------------------------------------------
    // Player Choice — called by Mb_RewardsPanelUI button callbacks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called when the player clicks the left card on the Rewards Panel.
    /// </summary>
    public void OnLeftChosen()
    {
        ApplyReward(_leftOption);
        CloseRewardsPanel();
    }


    /// <summary>
    /// Called when the player clicks the right card on the Rewards Panel.
    /// </summary>
    public void OnRightChosen()
    {
        ApplyReward(_rightOption);
        CloseRewardsPanel();
    }


    private void ApplyReward(RewardOption option)
    {
        if (option.Type == RewardType.AugmentSelection)
        {
            // Instantiate the correct derived augment class for the chosen SO
            Sc_AugmentBase augment = AugmentFactory.Create(option.AugmentData, GetPlayerCharacter());
            _augmentManager.AddAugment(augment);
        }

        // TODO: Handle RewardType.AbilityUpgrade and RewardType.UltimateBranch here
    }


    private void CloseRewardsPanel()
    {
        _RewardsPanelUI.Hide();
        GameManager.Instance.ChangeState(GameState.Playing);
    }


    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private Mb_CharacterBase GetPlayerCharacter()
    {
        return _PlayerObject.GetComponent<Mb_CharacterBase>();
    }

    //private int FindCompletedWaveIndex()
    //{
    //    // WaveManager tracks CurrentWaveIndex and increments it before spawning,
    //    // so after a wave ends, CurrentWaveIndex is the index of the wave that just finished.
    //    // We reach it via FindObjectOfType for now since WaveManager has no singleton.
    //    // TODO: Give Mb_WaveManager a static Instance property to avoid FindObjectOfType
    //    var waveManager = FindObjectOfType<Mb_WaveManager>();
    //    return waveManager != null ? waveManager.CurrentWaveIndex : -1;
    //}
}


// -------------------------------------------------------------------------
// Supporting Types — in this file for now, split into separate files if they grow
// -------------------------------------------------------------------------

/// <summary>
/// What kind of reward is being offered this wave.
/// </summary>
public enum RewardType
{
    AugmentSelection,
    AbilityUpgrade,
    UltimateBranch
}


/// <summary>
/// A single reward option passed to the UI for display.
/// The UI doesn't need to know what type it is — it just reads Name, Description, Icon.
/// The RewardsManager knows the type and uses it in ApplyReward().
/// </summary>
public struct RewardOption
{
    public RewardType Type;
    public string Name;
    public string Description;
    public UnityEngine.Sprite Icon;

    // The raw SO — held onto so ApplyReward can pass it to AugmentFactory
    // Null for non-augment reward types
    public SO_Augment AugmentData;

    public static RewardOption FromAugment(SO_Augment data)
    {
        return new RewardOption
        {
            Type = RewardType.AugmentSelection,
            Name = data.AugmentName,
            Description = data.Description,
            Icon = data.Icon,
            AugmentData = data,
        };
    }
}


/// <summary>
/// Maps SO_Augment assets to their concrete Sc_AugmentBase derived class.
/// When you add a new augment, add one line here.
/// This is the only place in the codebase that knows about every augment class.
/// </summary>
public static class AugmentFactory
{
    public static Sc_AugmentBase Create(SO_Augment data, Mb_CharacterBase owner)
    {
        // Match by augment name — the SO's AugmentName field must match exactly
        return data.AugmentName switch
        {
            "Wings of Balance" => new Augment_WingsOfBalance(data, owner),
            "Fight or Flight" => new Augment_FightOrFlight(data, owner),
            // TODO: Add remaining augments as their classes are implemented:
            // "Diya's Blessing"   => new Augment_DiyasBlessing(data, owner),
            // "Cycle of Life"     => new Augment_CycleOfLife(data, owner),
            // "Feral Surge"       => new Augment_FeralSurge(data, owner),
            // "Hunter's Instinct" => new Augment_HuntersInstinct(data, owner),
            // "Heart of the Forest" => new Augment_HeartOfTheForest(data, owner),
            // "Harmony's Tempo"   => new Augment_HarmonysTempo(data, owner),
            // "Primal Resonance"  => new Augment_PrimalResonance(data, owner),
            // "Natural Selection" => new Augment_NaturalSelection(data, owner),
            _ => throw new System.Exception($"[AugmentFactory] No class registered for augment: '{data.AugmentName}'")
        };
    }
}