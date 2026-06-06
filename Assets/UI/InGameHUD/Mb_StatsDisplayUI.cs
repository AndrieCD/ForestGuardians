// Mb_StatsDisplayUI.cs
// Displays ATK, AP, and HST stat values on the HUD via TMP_Text labels.
// Subscribes to Mb_StatBlock.OnStatsChanged to refresh automatically
// whenever any stat changes (augments, level-ups, modifiers).
//
// Inspector Setup:
//   - GuardianObject: drag the Guardian (Player) GameObject here.
//   - ATKText, APText, HSTText: drag the corresponding TMP_Text components.

using TMPro;
using UnityEngine;

public class Mb_StatsDisplayUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject GuardianObject;

    [Header("Stat Labels")]
    [SerializeField] private TMP_Text ATKText;
    [SerializeField] private TMP_Text APText;   
    [SerializeField] private TMP_Text CRIText;  // crit%
    [SerializeField] private TMP_Text HSTText;

    private Mb_StatBlock _statBlock;


    private void OnEnable()
    {
        if (GuardianObject == null)
        {
            Debug.LogError("[Mb_StatsDisplayUI] GuardianObject is not assigned.");
            return;
        }

        if (_statBlock == null)
            _statBlock = GuardianObject.GetComponent<Mb_StatBlock>();

        if (_statBlock == null)
        {
            Debug.LogError("[Mb_StatsDisplayUI] No Mb_StatBlock found on GuardianObject.");
            return;
        }

        _statBlock.OnStatsChanged -= Refresh;
        _statBlock.OnStatsChanged += Refresh;

        Refresh();
    }


    private void OnDisable()
    {
        if (_statBlock != null)
            _statBlock.OnStatsChanged -= Refresh;
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