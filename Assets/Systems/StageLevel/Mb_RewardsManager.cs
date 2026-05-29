// Mb_RewardsManager.cs
// Lives on the Stage GameObject alongside Mb_WaveManager and Mb_AugmentManager.
//
// REWARD SCHEDULE:
//   Reward types are configured per-wave on each SO_WaveData asset in the Inspector.
//   Set WaveReward on each wave to control what the player receives after that wave
//   clears. Set it to None to skip the panel entirely.
//
// REWARDS TIMER:
//   When the panel opens, a countdown coroutine starts. If the player does not choose
//   before RewardsTimeLimit seconds, the best available option is auto-selected:
//   left card if not maxed, right card otherwise. The timer is surfaced to
//   Mb_RewardsPanelUI via OnRewardsTimerTick so the panel can display a countdown.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mb_RewardsManager : MonoBehaviour
{
    #region Events                  //----------------------------------------

    public static event Action<RewardType> OnRewardsPanelOpened;
    public static event Action<RewardOption> OnRewardChosen;
    public static event Action OnRewardsPanelClosed;

    // Fired every second while the rewards panel is open.
    // Mb_RewardsPanelUI subscribes to this to update its countdown label.
    public static event Action<float> OnRewardsTimerTick;

    #endregion                      //----------------------------------------


    #region Inspector References    //----------------------------------------

    [Header("Stage Data")]
    [Tooltip("Assign the same SO_StageData asset used by Mb_WaveManager. " +
             "WaveReward on each SO_WaveData entry controls the reward schedule.")]
    [SerializeField] private SO_StageData _StageData;

    [Header("Rewards Timer")]
    [Tooltip("Seconds the player has to choose a reward before the best available " +
             "option is auto-selected. Left card is preferred unless it is a maxed placeholder.")]
    [SerializeField] private float RewardsTimeLimit = 10f;

    [Header("Augment Pool")]
    [SerializeField] private List<SO_Augment> _AugmentPool;

    [Header("UI")]
    [SerializeField] private Mb_RewardsPanelUI _RewardsPanelUI;

    [Header("References")]
    [SerializeField] private GameObject _PlayerObject;

    #endregion                      //----------------------------------------


    #region Runtime State           //----------------------------------------

    private Mb_AugmentManager _augmentManager;
    private Mb_AbilityController _abilityController;
    private Mb_GuardianBase _guardian;

    private HashSet<SO_Augment> _offeredAugments = new HashSet<SO_Augment>();
    private bool _branchSelected = false;

    private RewardOption _leftOption;
    private RewardOption _rightOption;

    // Tracked so we can stop the timer early when the player chooses manually
    private Coroutine _rewardsTimerCoroutine;

    // Flipped to true when the player (or auto-select) makes a choice —
    // stops the timer coroutine from applying a reward a second time
    private bool _choiceMade = false;

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Start()
    {
        _augmentManager = GetComponent<Mb_AugmentManager>();
        if (_augmentManager == null)
            Debug.LogError("[Mb_RewardsManager] Missing Mb_AugmentManager on this GameObject.");

        if (_StageData == null)
            Debug.LogError("[Mb_RewardsManager] No SO_StageData assigned. Rewards will not fire.");

        if (_PlayerObject != null)
        {
            _abilityController = _PlayerObject.GetComponent<Mb_AbilityController>();
            _guardian = _PlayerObject.GetComponent<Mb_GuardianBase>();
        }

        if (_abilityController == null)
            Debug.LogError("[Mb_RewardsManager] Could not find Mb_AbilityController on Player.");

        if (_guardian == null)
            Debug.LogError("[Mb_RewardsManager] Could not find Mb_GuardianBase on Player.");
    }

    private void OnEnable() => Mb_WaveManager.OnWaveEnd += HandleWaveEnd;
    private void OnDisable() => Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;

    #endregion                      //----------------------------------------


    #region Wave End Handler        //----------------------------------------

    private void HandleWaveEnd(int completedWaveIndex)
    {
        if (_StageData == null) return;

        if (completedWaveIndex < 0 || completedWaveIndex >= _StageData.WaveDataList.Count)
        {
            Debug.LogWarning($"[Mb_RewardsManager] completedWaveIndex {completedWaveIndex} is " +
                             $"out of range for WaveDataList (count: {_StageData.WaveDataList.Count}). Skipping.");
            return;
        }

        RewardType rewardType = _StageData.WaveDataList[completedWaveIndex].WaveReward;

        if (rewardType == RewardType.None) return;

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

    #endregion                      //----------------------------------------

    #region Panel Control           //----------------------------------------

    private void OpenRewardsPanel(RewardType rewardType)
    {
        OnRewardsPanelOpened?.Invoke(rewardType);

        bool optionsReady = TryBuildOptions(rewardType, out _leftOption, out _rightOption);

        if (!optionsReady)
        {
            Debug.LogWarning($"[Mb_RewardsManager] Could not build options for {rewardType}. Skipping.");
            // Fire closed immediately so WaveManager doesn't wait forever
            OnRewardsPanelClosed?.Invoke();
            return;
        }

        _choiceMade = false;

        // Subscribe to the panel's fade-complete event before showing —
        // this is how we know when to clean up game state
        _RewardsPanelUI.OnPanelFadeComplete += HandlePanelFadeComplete;

        GameManager.Instance.ChangeState(GameState.RewardsPanel);
        _RewardsPanelUI.Show(_leftOption, _rightOption);

        if (_rewardsTimerCoroutine != null)
            StopCoroutine(_rewardsTimerCoroutine);

        _rewardsTimerCoroutine = StartCoroutine(RewardsTimerRoutine());
    }


    // Cleans up game state after the panel animation finishes.
    // Fired by Mb_RewardsPanelUI.OnPanelFadeComplete for both
    // manual and auto-select paths.
    private void HandlePanelFadeComplete()
    {
        _RewardsPanelUI.OnPanelFadeComplete -= HandlePanelFadeComplete;

        OnRewardsPanelClosed?.Invoke();
        GameManager.Instance.ChangeState(GameState.Playing);
    }


    // Force-closes with no animation — scene teardown only
    private void CloseRewardsPanelImmediate()
    {
        if (_rewardsTimerCoroutine != null)
        {
            StopCoroutine(_rewardsTimerCoroutine);
            _rewardsTimerCoroutine = null;
        }

        _RewardsPanelUI.OnPanelFadeComplete -= HandlePanelFadeComplete;
        _RewardsPanelUI.Hide();

        OnRewardsPanelClosed?.Invoke();
        GameManager.Instance.ChangeState(GameState.Playing);
    }

    #endregion                      //----------------------------------------
    #region Rewards Timer           //----------------------------------------

    private IEnumerator RewardsTimerRoutine()
    {
        float remaining = RewardsTimeLimit;

        while (remaining > 0f)
        {
            OnRewardsTimerTick?.Invoke(remaining);
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        OnRewardsTimerTick?.Invoke(0f);

        if (_choiceMade) yield break;

        Debug.Log("[Mb_RewardsManager] Rewards timer expired — auto-selecting.");
        AutoSelectReward();
    }


    private void AutoSelectReward()
    {
        if (!_leftOption.IsMaxedPlaceholder)
        {
            Debug.Log("[Mb_RewardsManager] Auto-selected: Left.");
            ApplyReward(_leftOption);
            _RewardsPanelUI.ShowSelectionFeedback(RewardPanelSide.Left);
        }
        else
        {
            Debug.Log("[Mb_RewardsManager] Left is maxed — auto-selected: Right.");
            ApplyReward(_rightOption);
            _RewardsPanelUI.ShowSelectionFeedback(RewardPanelSide.Right);
        }
    }

    #endregion                      //----------------------------------------


    #region Option Building         //----------------------------------------

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

        int leftIndex = UnityEngine.Random.Range(0, available.Count);
        SO_Augment leftData = available[leftIndex];
        available.RemoveAt(leftIndex);
        SO_Augment rightData = available[UnityEngine.Random.Range(0, available.Count)];

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

        Sc_BaseAbility qAbility = _abilityController.GetAbilityBySlot(AbilitySlot.Q);
        Sc_BaseAbility eAbility = _abilityController.GetAbilityBySlot(AbilitySlot.E);

        bool qUpgradeable = qAbility != null && qAbility.CurrentLevel < qAbility.MaxLevel;
        bool eUpgradeable = eAbility != null && eAbility.CurrentLevel < eAbility.MaxLevel;

        if (!qUpgradeable && !eUpgradeable)
        {
            Debug.LogWarning("[Mb_RewardsManager] Both Q and E are at max level — skipping.");
            return false;
        }

        if (qUpgradeable && eUpgradeable)
        {
            left = RewardOption.FromAbilityUpgrade(qAbility, AbilitySlot.Q);
            right = RewardOption.FromAbilityUpgrade(eAbility, AbilitySlot.E);
            return true;
        }

        if (qUpgradeable)
        {
            left = RewardOption.FromAbilityUpgrade(qAbility, AbilitySlot.Q);
            right = RewardOption.MaxedPlaceholder(eAbility);
        }
        else
        {
            left = RewardOption.MaxedPlaceholder(qAbility);
            right = RewardOption.FromAbilityUpgrade(eAbility, AbilitySlot.E);
        }

        return true;
    }


    private bool TryBuildUltimateBranchOptions(out RewardOption left, out RewardOption right)
    {
        left = default;
        right = default;

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

    #endregion                      //----------------------------------------


    #region Player Choice           //----------------------------------------

    public void OnLeftChosen()
    {
        if (_choiceMade) return;
        _choiceMade = true;

        if (_rewardsTimerCoroutine != null)
        {
            StopCoroutine(_rewardsTimerCoroutine);
            _rewardsTimerCoroutine = null;
        }

        ApplyReward(_leftOption);
        _RewardsPanelUI.ShowSelectionFeedback(RewardPanelSide.Left);
    }

    public void OnRightChosen()
    {
        if (_choiceMade) return;
        _choiceMade = true;

        if (_rewardsTimerCoroutine != null)
        {
            StopCoroutine(_rewardsTimerCoroutine);
            _rewardsTimerCoroutine = null;
        }

        ApplyReward(_rightOption);
        _RewardsPanelUI.ShowSelectionFeedback(RewardPanelSide.Right);
    }


    private void ApplyReward(RewardOption option)
    {
        OnRewardChosen?.Invoke(option);

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
                    Debug.LogWarning("[Mb_RewardsManager] Tried to apply a maxed placeholder — ignoring.");
                    return;
                }
                _abilityController.LevelUpAbility(option.AbilitySlot);
                Debug.Log($"[Mb_RewardsManager] Upgraded ability in slot: {option.AbilitySlot}");
                break;

            case RewardType.UltimateBranch:
                Sc_BaseAbility branch = option.BranchOption.CreateAbility(GetPlayerCharacter());
                _abilityController.SetRSlot(branch);
                _branchSelected = true;
                Debug.Log($"[Mb_RewardsManager] Branch selected: {option.BranchOption.DisplayData.BranchName}");
                break;
        }
    }

    #endregion                      //----------------------------------------


    #region Helpers                 //----------------------------------------

    private Mb_CharacterBase GetPlayerCharacter()
    {
        return _PlayerObject.GetComponent<Mb_CharacterBase>();
    }

    #endregion                      //----------------------------------------
}


// -------------------------------------------------------------------------
// Supporting Types
// -------------------------------------------------------------------------

public enum RewardType
{
    None,
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
    public AbilitySlot AbilitySlot;
    public bool IsMaxedPlaceholder;

    // UltimateBranch
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

    public static RewardOption FromAbilityUpgrade(Sc_BaseAbility ability, AbilitySlot slot)
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
            "Harmony's Tempo" => new Mb_HarmonysTempo(data, owner),
            "Feral Surge" => new Augment_FeralSurge(data, owner),
            "Heart of the Forest" => new Augment_HeartOfTheForest(data, owner),
            "Fight or Flight" => new Augment_FightOrFlight(data, owner),
            "Diya's Blessing" => new Mb_DiyasBlessing(data, owner),
            "Cycle of Life" => new Mb_CycleOfLife(data, owner),
            "Primal Resonance" => new Mb_PrimalResonance(data, owner),
            "Natural Selection" => new Mb_NaturalSelection(data, owner),
            "Hunter's Instinct" => new Mb_HuntersInstinct(data, owner),

            _ => throw new System.Exception(
                $"[AugmentFactory] No class registered for augment: '{data.AugmentName}'")
        };
    }
}