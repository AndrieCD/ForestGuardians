using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Mb_ForestBlessingCollectible : MonoBehaviour
{
    private Sc_ForestBlessingData _blessingData;
    private Mb_ForestBlessingSpawner _spawner;
    private float _despawnDuration;
    private bool _collected;
    private Coroutine _despawnCoroutine;

    public void Initialize(Sc_ForestBlessingData blessingData, Mb_ForestBlessingSpawner spawner, float despawnDuration)
    {
        _blessingData = blessingData;
        _spawner = spawner;
        _despawnDuration = Mathf.Max(0.1f, despawnDuration);
        _collected = false;

        Collider blessingCollider = GetComponent<Collider>();
        blessingCollider.isTrigger = true;

        if (_despawnCoroutine != null)
            StopCoroutine(_despawnCoroutine);

        _despawnCoroutine = StartCoroutine(DespawnRoutine());
    }

    private void OnDisable()
    {
        if (_despawnCoroutine != null)
        {
            StopCoroutine(_despawnCoroutine);
            _despawnCoroutine = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;

        Mb_GuardianBase guardian = other.GetComponentInParent<Mb_GuardianBase>();
        if (guardian == null || guardian != Mb_GuardianBase.CurrentGuardian) return;

        if (!TryApplyBlessing(guardian))
            return;

        _collected = true;
        _spawner?.NotifyBlessingRemoved(this);
        Destroy(gameObject);
    }

    private bool TryApplyBlessing(Mb_GuardianBase guardian)
    {
        if (_blessingData == null)
        {
            Debug.LogError("[Mb_ForestBlessingCollectible] Cannot apply Blessing because no Blessing data is assigned.");
            return false;
        }

        if (guardian.Stats == null)
        {
            Debug.LogError("[Mb_ForestBlessingCollectible] Cannot apply Blessing because the Guardian has no StatBlock.");
            return false;
        }

        List<Sc_StatEffect> effects = new List<Sc_StatEffect>();
        foreach (Sc_StatEffect effect in _blessingData.StatEffects)
        {
            if (effect == null) continue;
            effects.Add(new Sc_StatEffect(effect.TargetStat, effect.Value, effect.Type));
        }

        float duration = _blessingData.GetDuration();

        if (effects.Count > 0)
        {
            Sc_Modifier modifier = new Sc_Modifier(
                $"Forest Blessing - {_blessingData.BlessingName}",
                ModifierSource.ForestBlessing,
                effects,
                duration
            );

            guardian.Stats.AddModifier(modifier);
        }

        ApplyShield(guardian, duration);

        Debug.Log($"[Mb_ForestBlessingCollectible] Applied {_blessingData.BlessingName} for {duration:0.##} seconds.");
        return true;
    }

    private void ApplyShield(Mb_GuardianBase guardian, float duration)
    {
        if (guardian.Health == null) return;

        float shieldAmount = Mathf.Max(0f, _blessingData.ShieldFlatAmount);

        if (_blessingData.ShieldMaxHealthPercent > 0f && guardian.Stats != null && guardian.Stats.MaxHealth != null)
            shieldAmount += guardian.Stats.MaxHealth.GetValue() * _blessingData.ShieldMaxHealthPercent;

        if (shieldAmount <= 0f) return;

        guardian.Health.AddShield(shieldAmount, duration);
    }

    private IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(_despawnDuration);

        if (_collected) yield break;

        _spawner?.NotifyBlessingRemoved(this);
        Destroy(gameObject);
    }
}
