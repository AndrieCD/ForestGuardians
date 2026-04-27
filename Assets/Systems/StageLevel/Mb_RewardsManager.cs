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

    [Header("UI")]
    [SerializeField] private Mb_RewardsPanelUI _RewardsPanelUI;

    [Header("References")]
    [SerializeField] private GameObject _PlayerObject;


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    private Mb_AugmentManager _augmentManager;
    private Mb_AbilityController _abilityController;

    // Typed as Mb_GuardianBase — works for any guardian, not just Rajah.
    // GetBranchOptions() is defined on the base class so no cast is needed.
    private Mb_GuardianBase _guardian;

    private HashSet<SO_Augment> _offeredAugments = new HashSet<SO_Augment>();
    private bool _branchSelected = false;

    private RewardOption _leftOption;
    private RewardOption _rightOption;


    // -------------------------------------------------------------------------
    // Wave Reward Schedule
    // -------------------------------------------------------------------------

    private static readonly Dictionary<int, RewardType> REWARD_SCHEDULE = new Dictionary<int, RewardType>
    {
        { 0,  RewardType.AbilityUpgrade   },
        { 1,  RewardType.AbilityUpgrade   },
        { 3,  RewardType.AugmentSelection },
        { 4,  RewardType.UltimateBranch   },
        { 5,  RewardType.AbilityUpgrade   },
        { 6,  RewardType.AbilityUpgrade   },
        { 8,  RewardType.AugmentSelection },
        { 9,  RewardType.AbilityUpgrade   },
        { 10, RewardType.AbilityUpgrade   },
        { 11, RewardType.AbilityUpgrade   },
        { 12, RewardType.AugmentSelection },
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

            // Fetch as Mb_GuardianBase — any guardian subclass satisfies this
            _guardian = _PlayerObject.GetComponent<Mb_GuardianBase>();
        }

        if (_abilityController == null)
            Debug.LogError("[Mb_RewardsManager] Could not find Mb_AbilityController on Player.");

        if (_guardian == null)
            Debug.LogError("[Mb_RewardsManager] Could not find Mb_GuardianBase on Player.");
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

        if (rewardType == RewardType.AugmentSelection && !_augmentManager.CanEquipMore)
        {
            Debug.Log("[Mb_RewardsManager] Augment reward skipped — player has max augments.");
            return;
        }

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
            if (!_offeredAugments.Contains(augment))
                available.Add(augment);

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

        Sc_BaseAbility qAbility = _abilityController.GetAbilityBySlot("Q");
        Sc_BaseAbility eAbility = _abilityController.GetAbilityBySlot("E");

        bool qUpgradeable = qAbility != null && qAbility.CurrentLevel < qAbility.MaxLevel;
        bool eUpgradeable = eAbility != null && eAbility.CurrentLevel < eAbility.MaxLevel;

        if (!qUpgradeable && !eUpgradeable)
        {
            Debug.LogWarning("[Mb_RewardsManager] Both Q and E are at max level — skipping.");
            return false;
        }

        if (qUpgradeable && eUpgradeable)
        {
            left = RewardOption.FromAbilityUpgrade(qAbility, "Q");
            right = RewardOption.FromAbilityUpgrade(eAbility, "E");
            return true;
        }

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

        // Ask the guardian for its own branch definitions —
        // no factory, no string matching, no guardian-specific code here
        var (branch1, branch2) = _guardian.GetBranchOptions();

        if (branch1 == null || branch2 == null)
        {
            Debug.LogError("[Mb_RewardsManager] Guardian returned null branch options. " +
                           "Make sure DefineBranches() is overridden and all SO fields are assigned.");
            return false;
        }

        if (branch1.DisplayData == null || branch2.DisplayData == null)
        {
            Debug.LogError("[Mb_RewardsManager] A branch option has no DisplayData assigned. " +
                           "Check BranchDisplay1 and BranchDisplay2 on the SO_Guardian.");
            return false;
        }

        left = RewardOption.FromBranchOption(branch1);
        right = RewardOption.FromBranchOption(branch2);
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
                Sc_AugmentBase augment = AugmentFactory.Create(
                    option.AugmentData,
                    GetPlayerCharacter()
                );
                _augmentManager.AddAugment(augment);
                break;

            case RewardType.AbilityUpgrade:
                if (option.IsMaxedPlaceholder)
                {
                    Debug.LogWarning("[Mb_RewardsManager] Player clicked a maxed ability card — ignoring.");
                    return;
                }
                _abilityController.LevelUpAbility(option.AbilitySlot);
                Debug.Log($"[Mb_RewardsManager] Upgraded ability in slot: {option.AbilitySlot}");
                break;

            case RewardType.UltimateBranch:
                // The branch option already knows how to create its ability —
                // call the delegate with the player as the owner
                Sc_BaseAbility branch = option.BranchOption.CreateAbility(GetPlayerCharacter());
                _abilityController.SetRSlot(branch);
                _branchSelected = true;
                Debug.Log($"[Mb_RewardsManager] Branch selected: {option.BranchOption.DisplayData.BranchName}");
                break;
        }
    }


    private void CloseRewardsPanel()
    {
        _RewardsPanelUI.Hide();
        GameManager.Instance.ChangeState(GameState.Playing);
    }


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


public struct RewardOption
{
    public RewardType Type;
    public string Name;
    public string Description;
    public Sprite Icon;

    // AugmentSelection
    public SO_Augment AugmentData;

    // AbilityUpgrade
    public string AbilitySlot;
    public bool IsMaxedPlaceholder;

    // UltimateBranch — holds the full branch option including its factory delegate
    // RewardsManager calls BranchOption.CreateAbility(owner) directly on selection
    public Sc_BranchOption BranchOption;


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


    public static RewardOption FromAbilityUpgrade(Sc_BaseAbility ability, string slot)
    {
        SO_Ability abilityData = ability.GetAbilityData();
        int nextLevel = ability.CurrentLevel + 1;

        return new RewardOption
        {
            Type = RewardType.AbilityUpgrade,
            Name = $"{abilityData.AbilityName} — Level {nextLevel}",
            Description = $"Upgrade {abilityData.AbilityName} from " +
                                 $"Level {ability.CurrentLevel} to Level {nextLevel}.",
            Icon = abilityData.Icon,
            AbilitySlot = slot,
            IsMaxedPlaceholder = false,
        };
    }


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


    /// <summary>
    /// Builds a reward card from a Sc_BranchOption.
    /// Display data (name, description, icon) comes from the branch's SO_UltimateBranch.
    /// The full BranchOption is stored so ApplyReward can call CreateAbility() directly.
    /// </summary>
    public static RewardOption FromBranchOption(Sc_BranchOption branch)
    {
        return new RewardOption
        {
            Type = RewardType.UltimateBranch,
            Name = branch.DisplayData.BranchName,
            Description = branch.DisplayData.Description,
            Icon = branch.DisplayData.Icon,
            BranchOption = branch,
        };
    }
}


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