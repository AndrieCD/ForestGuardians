// Mb_RewardsManager.cs
// Lives on the Stage GameObject alongside Mb_WaveManager and Mb_AugmentManager.

using System.Collections.Generic;
using UnityEngine;

public class Mb_RewardsManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector References
    // -------------------------------------------------------------------------

    [Header("Augment Pool")]
    [SerializeField] private List<SO_Augment> _AugmentPool;

    [Header("Ultimate Branch Options")]
    // Drag the two SO_UltimateBranch assets for this guardian here.
    // Branch1 = left card, Branch2 = right card — always presented in this order.
    [SerializeField] private SO_UltimateBranch _RBranch1Data;
    [SerializeField] private SO_UltimateBranch _RBranch2Data;

    [Header("UI")]
    [SerializeField] private Mb_RewardsPanelUI _RewardsPanelUI;

    [Header("References")]
    [SerializeField] private GameObject _PlayerObject;


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    private Mb_AugmentManager _augmentManager;
    private Mb_AbilityController _abilityController;
    private Mb_PlayerController _playerController;

    private HashSet<SO_Augment> _offeredAugments = new HashSet<SO_Augment>();

    // Prevents the ultimate branch panel from ever opening a second time
    private bool _branchSelected = false;

    private RewardOption _leftOption;
    private RewardOption _rightOption;


    // -------------------------------------------------------------------------
    // Wave Reward Schedule
    // Key = completed wave index (0-based). Waves not listed have no reward.
    // -------------------------------------------------------------------------
    private static readonly Dictionary<int, RewardType> REWARD_SCHEDULE = new Dictionary<int, RewardType>
    {
        { 0,  RewardType.AbilityUpgrade   },    // After Wave 1
        { 1,  RewardType.AbilityUpgrade   },    // After Wave 2
        { 3,  RewardType.AugmentSelection },    // After Wave 4
        { 4,  RewardType.UltimateBranch   },    // After Wave 5
        { 5,  RewardType.AbilityUpgrade   },    // After Wave 6
        { 6,  RewardType.AbilityUpgrade   },    // After Wave 7
        { 8,  RewardType.AugmentSelection },    // After Wave 9
        { 9,  RewardType.AbilityUpgrade   },    // After Wave 10
        { 10, RewardType.AbilityUpgrade   },    // After Wave 11
        { 11, RewardType.AbilityUpgrade   },    // After Wave 12
        { 12, RewardType.AugmentSelection },    // After Wave 13
    };


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        _augmentManager = GetComponent<Mb_AugmentManager>();
        if (_augmentManager == null)
            Debug.LogError("[Mb_RewardsManager] Missing Mb_AugmentManager on this GameObject.");

        if (_PlayerObject != null)
        {
            _abilityController = _PlayerObject.GetComponent<Mb_AbilityController>();
            _playerController = _PlayerObject.GetComponent<Mb_PlayerController>();
        }

        if (_abilityController == null)
            Debug.LogError("[Mb_RewardsManager] Could not find Mb_AbilityController on Player.");

        if (_playerController == null)
            Debug.LogError("[Mb_RewardsManager] Could not find Mb_PlayerController on Player.");
    }


    private void OnEnable() => Mb_WaveManager.OnWaveEnd += HandleWaveEnd;
    private void OnDisable() => Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;


    // -------------------------------------------------------------------------
    // Wave End Handler
    // -------------------------------------------------------------------------

    private void HandleWaveEnd(int completedWaveIndex)
    {
        if (!REWARD_SCHEDULE.TryGetValue(completedWaveIndex, out RewardType rewardType))
            return;

        // Skip augment reward if player already has max augments
        if (rewardType == RewardType.AugmentSelection && !_augmentManager.CanEquipMore)
        {
            Debug.Log("[Mb_RewardsManager] Augment reward skipped — player has max augments.");
            return;
        }

        // Skip ultimate branch if already chosen — should never happen with the schedule,
        // but this guard prevents any edge-case double-trigger
        if (rewardType == RewardType.UltimateBranch && _branchSelected)
        {
            Debug.Log("[Mb_RewardsManager] Ultimate branch already selected — skipping.");
            return;
        }

        _PlayerObject.GetComponent<Mb_CharacterBase>().LevelUp();

        OpenRewardsPanel(rewardType);
    }


    private void OpenRewardsPanel(RewardType rewardType)
    {
        bool optionsReady = TryBuildOptions(rewardType, out _leftOption, out _rightOption);

        if (!optionsReady)
        {
            Debug.LogWarning($"[Mb_RewardsManager] Could not build options for {rewardType}. Skipping.");
            return;
        }

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

        return rewardType switch
        {
            RewardType.AugmentSelection => TryBuildAugmentOptions(out left, out right),
            RewardType.AbilityUpgrade => TryBuildAbilityUpgradeOptions(out left, out right),
            RewardType.UltimateBranch => TryBuildUltimateBranchOptions(out left, out right),
            _ => false
        };
    }


    private bool TryBuildAugmentOptions(out RewardOption left, out RewardOption right)
    {
        left = default;
        right = default;

        List<SO_Augment> available = new List<SO_Augment>();
        foreach (var augment in _AugmentPool)
        {
            if (!_offeredAugments.Contains(augment))
                available.Add(augment);
        }

        if (available.Count < 2)
        {
            Debug.LogWarning("[Mb_RewardsManager] Not enough un-offered augments to fill the panel.");
            return false;
        }

        int leftIndex = Random.Range(0, available.Count);
        SO_Augment leftData = available[leftIndex];
        available.RemoveAt(leftIndex);
        SO_Augment rightData = available[Random.Range(0, available.Count)];

        _offeredAugments.Add(leftData);
        _offeredAugments.Add(rightData);

        left = RewardOption.FromAugment(leftData);
        right = RewardOption.FromAugment(rightData);
        return true;
    }


    private bool TryBuildAbilityUpgradeOptions(out RewardOption left, out RewardOption right)
    {
        left = default;
        right = default;

        // Fetch the Q and E abilities from the controller so we can check their levels
        Sc_BaseAbility qAbility = _abilityController.GetAbilityBySlot("Q");
        Sc_BaseAbility eAbility = _abilityController.GetAbilityBySlot("E");

        bool qUpgradeable = qAbility != null && qAbility.CurrentLevel < qAbility.MaxLevel;
        bool eUpgradeable = eAbility != null && eAbility.CurrentLevel < eAbility.MaxLevel;

        // Need at least one upgradeable ability to show the panel
        if (!qUpgradeable && !eUpgradeable)
        {
            Debug.LogWarning("[Mb_RewardsManager] Both Q and E are at max level — skipping upgrade reward.");
            return false;
        }

        // Both upgradeable: offer Q on the left, E on the right
        if (qUpgradeable && eUpgradeable)
        {
            left = RewardOption.FromAbilityUpgrade(qAbility, "Q");
            right = RewardOption.FromAbilityUpgrade(eAbility, "E");
            return true;
        }

        // Only one is upgradeable — put it on the left, show a "Max Level" placeholder on the right
        // This keeps the two-card layout consistent even in edge cases
        if (qUpgradeable)
        {
            left = RewardOption.FromAbilityUpgrade(qAbility, "Q");
            right = RewardOption.MaxedPlaceholder(eAbility);
        }
        else
        {
            left = RewardOption.MaxedPlaceholder(qAbility);
            right = RewardOption.FromAbilityUpgrade(eAbility, "E");
        }

        return true;
    }


    private bool TryBuildUltimateBranchOptions(out RewardOption left, out RewardOption right)
    {
        left = default;
        right = default;

        if (_RBranch1Data == null || _RBranch2Data == null)
        {
            Debug.LogError("[Mb_RewardsManager] Ultimate branch SOs are not assigned in the Inspector.");
            return false;
        }

        // Branch 1 always on the left, Branch 2 always on the right —
        // consistent positioning helps players who read patch notes or guides
        left = RewardOption.FromUltimateBranch(_RBranch1Data);
        right = RewardOption.FromUltimateBranch(_RBranch2Data);
        return true;
    }


    // -------------------------------------------------------------------------
    // Player Choice
    // -------------------------------------------------------------------------

    public void OnLeftChosen()
    {
        ApplyReward(_leftOption);
        CloseRewardsPanel();
    }

    public void OnRightChosen()
    {
        ApplyReward(_rightOption);
        CloseRewardsPanel();
    }


    private void ApplyReward(RewardOption option)
    {
        switch (option.Type)
        {
            case RewardType.AugmentSelection:
                Sc_AugmentBase augment = AugmentFactory.Create(option.AugmentData, GetPlayerCharacter());
                _augmentManager.AddAugment(augment);
                break;

            case RewardType.AbilityUpgrade:
                // If the player somehow clicks a maxed placeholder card, do nothing
                if (option.IsMaxedPlaceholder)
                {
                    Debug.LogWarning("[Mb_RewardsManager] Player clicked a maxed ability card — ignoring.");
                    return;
                }
                _abilityController.LevelUpAbility(option.AbilitySlot);
                Debug.Log($"[Mb_RewardsManager] Upgraded ability in slot: {option.AbilitySlot}");
                break;

            case RewardType.UltimateBranch:
                // Get the R ability SO from the player controller — holds cooldown and scaling
                SO_Ability rAbilityData = _playerController.GetRAbilityData();

                if (rAbilityData == null)
                {
                    Debug.LogError("[Mb_RewardsManager] Guardian template has no RAbility SO assigned.");
                    return;
                }

                // Instantiate the correct branch class and assign it to the R slot
                Sc_BaseAbility branch = UltimateBranchFactory.Create(
                    option.BranchData,
                    rAbilityData,
                    GetPlayerCharacter()
                );

                _abilityController.SetRSlot(branch);

                // Lock the branch — this reward can never be offered again
                _branchSelected = true;

                Debug.Log($"[Mb_RewardsManager] Ultimate branch selected: {option.BranchData.BranchName}");
                break;
        }
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
}


// -------------------------------------------------------------------------
// Supporting Types
// -------------------------------------------------------------------------

public enum RewardType
{
    AugmentSelection,
    AbilityUpgrade,
    UltimateBranch
}


/// <summary>
/// A single reward option passed to the UI for display.
/// The UI reads Name, Description, and Icon — it never needs to know the reward type.
/// RewardsManager uses Type and the type-specific fields in ApplyReward().
/// </summary>
public struct RewardOption
{
    public RewardType Type;
    public string Name;
    public string Description;
    public Sprite Icon;

    // Set for AugmentSelection — the SO to pass to AugmentFactory
    public SO_Augment AugmentData;

    // Set for AbilityUpgrade — which slot to level up ("Q" or "E")
    public string AbilitySlot;

    // Set for AbilityUpgrade — true when the ability is already maxed,
    // so the UI can show a disabled/greyed card and ApplyReward ignores the click
    public bool IsMaxedPlaceholder;

    // Set for UltimateBranch — the SO to pass to UltimateBranchFactory
    public SO_UltimateBranch BranchData;


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


    /// <summary>
    /// Builds an upgrade card for the given ability slot.
    /// Description is generated dynamically from the ability's current and next level
    /// so the player can see exactly what they're getting.
    /// </summary>
    public static RewardOption FromAbilityUpgrade(Sc_BaseAbility ability, string slot)
    {
        // Pull the SO data for display — name and icon live there
        // We reach the SO via the ability's internal reference using the base class accessor
        SO_Ability abilityData = ability.GetAbilityData();

        int nextLevel = ability.CurrentLevel + 1;

        return new RewardOption
        {
            Type = RewardType.AbilityUpgrade,
            Name = $"{abilityData.AbilityName} — Level {nextLevel}",
            Description = $"Upgrade {abilityData.AbilityName} from Level {ability.CurrentLevel} to Level {nextLevel}.",
            Icon = abilityData.Icon,
            AbilitySlot = slot,
            IsMaxedPlaceholder = false,
        };
    }


    /// <summary>
    /// Builds a disabled placeholder card for an ability that is already at max level.
    /// Shown when only one of Q/E is still upgradeable, to keep the two-card layout intact.
    /// </summary>
    public static RewardOption MaxedPlaceholder(Sc_BaseAbility ability)
    {
        SO_Ability abilityData = ability.GetAbilityData();

        return new RewardOption
        {
            Type = RewardType.AbilityUpgrade,
            Name = $"{abilityData.AbilityName} — MAX",
            Description = "Already at maximum level.",
            Icon = abilityData.Icon,
            IsMaxedPlaceholder = true,
        };
    }


    public static RewardOption FromUltimateBranch(SO_UltimateBranch data)
    {
        return new RewardOption
        {
            Type = RewardType.UltimateBranch,
            Name = data.BranchName,
            Description = data.Description,
            Icon = data.Icon,
            BranchData = data,
        };
    }
}


/// <summary>
/// Maps SO_UltimateBranch assets to their concrete Sc_BaseAbility derived class.
/// When you add a new guardian, add their branch entries here.
/// </summary>
public static class UltimateBranchFactory
{
    public static Sc_BaseAbility Create(
        SO_UltimateBranch branchData,
        SO_Ability abilityData,
        Mb_CharacterBase owner)
    {
        return branchData.BranchName switch
        {
            "Sovereign's Wrath" => new Rajah_R_Branch1(abilityData, owner),
            "Eagle Eye" => new Rajah_R_Branch2(abilityData, owner),
            _ => throw new System.Exception(
                $"[UltimateBranchFactory] No class registered for branch: '{branchData.BranchName}'")
        };
    }
}


/// <summary>
/// Maps SO_Augment assets to their concrete Sc_AugmentBase derived class.
/// </summary>
public static class AugmentFactory
{
    public static Sc_AugmentBase Create(SO_Augment data, Mb_CharacterBase owner)
    {
        return data.AugmentName switch
        {
            "Wings of Balance" => new Augment_WingsOfBalance(data, owner),
            "Fight or Flight" => new Augment_FightOrFlight(data, owner),
            // TODO: remaining augments
            _ => throw new System.Exception(
                $"[AugmentFactory] No class registered for augment: '{data.AugmentName}'")
        };
    }
}