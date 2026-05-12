// Augment_DiyasBlessing.cs
// Passively steal HP from all nearby enemies on a fixed timer tick.
// Each tick: deal (2% of player's Max HP) to every enemy in range,
// and heal the player by the same amount per enemy hit.
//
// Range is based on the player's BASE Max HP at level 1 so it stays
// consistent and doesn't grow into absurdity as Max HP scales.
//
// HOW IT WORKS:
//   - OnEquip starts a coroutine loop on the owner MonoBehaviour.
//   - Every TICK_INTERVAL seconds, Physics.OverlapSphere finds nearby CuBots.
//   - Each CuBot in range takes damage; player heals by the same amount.
//   - OnUnequip stops the loop via a cancellation flag.
//
// Inspector setup: none required. Tune RANGE_DIVISOR and TICK_INTERVAL below.

using System.Collections;
using UnityEngine;

public class Mb_DiyasBlessing : Sc_AugmentBase
{
    // How often the steal pulse fires, in seconds.
    // TODO: Tune in Inspector — 1.5s feels about right for a passive pulse.
    private const float TICK_INTERVAL = 1.0f;

    // Range = baseMaxHP / RANGE_DIVISOR.
    // A divisor of ~20 on a 1000 base HP guardian gives ~50 units — just
    // outside melee range. Raise the divisor to shrink range, lower it to expand.
    // TODO: Tune this constant based on actual scene scale and melee attack range.
    private const float RANGE_DIVISOR = 250f;

    // Percentage of current Max HP dealt as damage (and healed) per tick per enemy.
    private const float STEAL_PERCENT = 0.02f;

    // Set to false on unequip to stop the coroutine cleanly without StopCoroutine
    // (StopCoroutine needs the exact IEnumerator reference; a flag is simpler and safer)
    private bool _isActive = false;

    // The computed range — fixed at equip time from base Max HP
    private float _range;


    public Mb_DiyasBlessing(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // This augment has no static SO effects — skip base.OnEquip().

        // Read the BASE Max HP (before any modifiers) so range doesn't
        // spiral upward as the player gains Max HP from other augments.
        // BaseValue is the raw SO value — it's always the level-1 floor.
        _range = owner.Stats.MaxHealth.BaseValue / RANGE_DIVISOR;

        _isActive = true;

        // Start the steal loop on the owner — they're a MonoBehaviour so
        // StartCoroutine is available through them.
        owner.StartCoroutine(StealLoop(owner));

        Debug.Log($"[Diya's Blessing] Equipped. Steal range: {_range} units.");
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        // Flipping the flag stops the coroutine at its next iteration —
        // we don't need to find and kill the coroutine explicitly.
        _isActive = false;
    }


    private IEnumerator StealLoop(Mb_CharacterBase owner)
    {
        while (_isActive)
        {
            yield return new WaitForSeconds(TICK_INTERVAL);

            // Re-check after the wait in case unequip happened while we were sleeping
            if (!_isActive) yield break;

            ExecuteStealTick(owner);
        }
    }


    private void ExecuteStealTick(Mb_CharacterBase owner)
    {
        // How much to steal per enemy this tick — 5% of current Max HP
        float stealAmount = owner.Stats.MaxHealth.GetValue() * STEAL_PERCENT;

        // Find all colliders within range of the player
        // TODO: Replace Physics.AllLayers with a dedicated "Enemy" layer mask
        //       once layers are configured in the project. This avoids hitting
        //       terrain, projectiles, or other non-enemy objects.
        Collider[] hits = Physics.OverlapSphere(
            owner.transform.position,
            _range
        );

        int enemiesHit = 0;

        foreach (Collider hit in hits)
        {
            // Only steal from CuBots — check for the base class component
            MB_CuBotBase cuBot = hit.GetComponent<MB_CuBotBase>();
            if (cuBot == null) continue;
            if (cuBot.Health.IsDead) continue;

            // Deal damage to the enemy
            // SetLastAttacker so a killing blow here credits the player for Cycle of Life
            cuBot.SetLastAttacker(owner);
            cuBot.Health.TakeDamage(stealAmount);

            enemiesHit++;
        }

        if (enemiesHit > 0)
        {
            // Heal the player once per tick — total heal = stealAmount * enemies hit
            float totalHeal = stealAmount * enemiesHit;
            owner.Health.Heal(totalHeal);

            // TODO: Spawn or play a VFX here to show the steal pulse visually.
            // Suggested hook:
            //   VFXManager.Instance.PlayDiyaStealPulse(owner.transform.position, _range);
            // Or trigger a particle system parented to the player:
            //   owner.GetComponentInChildren<ParticleSystem>()?.Play();
            // The pulse should be a radial effect expanding from the player outward.

            Debug.Log($"[Diya's Blessing] Stole {stealAmount} from {enemiesHit} enemies. Healed {totalHeal}.");
        }
    }
}