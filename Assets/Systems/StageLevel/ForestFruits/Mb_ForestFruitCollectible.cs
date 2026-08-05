using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Mb_ForestFruitCollectible : MonoBehaviour
{
    private Mb_RewardsManager _rewardsManager;
    private Mb_ForestFruitSpawner _spawner;
    private bool _collected;

    public void Initialize(Mb_RewardsManager rewardsManager, Mb_ForestFruitSpawner spawner)
    {
        _rewardsManager = rewardsManager;
        _spawner = spawner;
        _collected = false;

        Collider fruitCollider = GetComponent<Collider>();
        fruitCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;

        Mb_GuardianBase guardian = other.GetComponentInParent<Mb_GuardianBase>();
        if (guardian == null || guardian != Mb_GuardianBase.CurrentGuardian) return;

        if (_rewardsManager == null)
        {
            Debug.LogError("[Mb_ForestFruitCollectible] Cannot collect fruit because no RewardsManager is assigned.");
            return;
        }

        if (!_rewardsManager.TryOpenForestFruitRewards())
            return;

        _collected = true;
        _spawner?.NotifyFruitCollected(this);
        Destroy(gameObject);
    }
}
