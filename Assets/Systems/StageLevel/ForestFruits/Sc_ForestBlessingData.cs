using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Sc_ForestBlessingData
{
    public string BlessingName = "Forest Blessing";
    public float EffectDuration = 5f;
    public List<Sc_StatEffect> StatEffects = new List<Sc_StatEffect>();

    [Header("Shield")]
    public float ShieldFlatAmount = 0f;
    public float ShieldMaxHealthPercent = 0f;

    public Sc_ForestBlessingData() { }

    public Sc_ForestBlessingData(string blessingName, float effectDuration, List<Sc_StatEffect> statEffects)
    {
        BlessingName = blessingName;
        EffectDuration = effectDuration;
        StatEffects = statEffects;
    }

    public float GetDuration()
    {
        return Mathf.Max(0.1f, EffectDuration);
    }
}
