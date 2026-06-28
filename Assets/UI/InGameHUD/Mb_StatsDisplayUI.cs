// Mb_StatsDisplayUI.cs
// Displays ATK, AP, and HST stat values on the HUD via TMP_Text labels.
// Subscribes to Mb_StatBlock.OnStatsChanged to refresh automatically
// whenever any stat changes (augments, level-ups, modifiers).
//
// Inspector Setup:
//   - ATKText, APText, CRIText, HSTText: drag the corresponding TMP_Text components.
//   - Guardian binding is automatic through Mb_GuardianBase.CurrentGuardian.

using TMPro;
using UnityEngine;

public class Mb_StatsDisplayUI : MonoBehaviour
{
    [Header("Stat Labels")]
    [SerializeField] private TMP_Text ATKText;
    [SerializeField] private TMP_Text APText;   
    [SerializeField] private TMP_Text CRIText;  // crit%
    [SerializeField] private TMP_Text HSTText;

    private Mb_StatBlock _statBlock;


    private void OnEnable()
    {
        Mb_GuardianBase.OnActiveGuardianChanged -= HandleActiveGuardianChanged;
        Mb_GuardianBase.OnActiveGuardianChanged += HandleActiveGuardianChanged;

        BindGuardian(Mb_GuardianBase.CurrentGuardian);
    }


    private void OnDisable()
    {
        Mb_GuardianBase.OnActiveGuardianChanged -= HandleActiveGuardianChanged;
        UnbindStatBlock();
    }

    private void HandleActiveGuardianChanged(Mb_GuardianBase guardian)
    {
        BindGuardian(guardian);
    }

    private void BindGuardian(Mb_GuardianBase guardian)
    {
        UnbindStatBlock();

        if (guardian == null)
            return;

        _statBlock = guardian.Stats;

        if (_statBlock == null)
        {
            Debug.LogError($"[Mb_StatsDisplayUI] No Mb_StatBlock found on {guardian.gameObject.name}.");
            return;
        }

        _statBlock.OnStatsChanged += Refresh;
        Refresh();
    }

    private void UnbindStatBlock()
    {
        if (_statBlock != null)
            _statBlock.OnStatsChanged -= Refresh;

        _statBlock = null;
    }


    private void Refresh()
    {
        if (_statBlock == null) return;

        if (ATKText != null)
            ATKText.text = $"{Mathf.RoundToInt(_statBlock.AttackPower.GetValue())}";

        if (APText != null)
            APText.text = $"{Mathf.RoundToInt(_statBlock.AbilityPower.GetValue())}";

        if (CRIText != null)
            CRIText.text = $"{Mathf.RoundToInt(_statBlock.CriticalChance.GetValue() * 100f)}%";

        if (HSTText != null)
            HSTText.text = $"{Mathf.RoundToInt(_statBlock.Haste.GetValue())}";
    }
}
