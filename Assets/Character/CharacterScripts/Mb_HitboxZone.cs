// Mb_HitboxZone.cs
// A reusable trigger-collider component that sits on a hitbox child GameObject.
// Abilities enable this collider for their hit window, then disable it.
//
// HOW IT WORKS:
//   - Attach this to any hitbox child GameObject (e.g. "MeleeHitbox", "RAbilityHitbox")
//   - The Collider on the same GameObject must be set to isTrigger = true
//   - When the Collider is enabled, OnTriggerEnter fires for overlapping objects
//   - Each MB_CuBotBase that enters fires OnHit exactly once per activation
//   - The HashSet is cleared automatically when the collider is disabled,
//     so the next activation starts clean
//
// ABILITY USAGE PATTERN:
//   1. Subscribe to hitboxZone.OnHit before enabling
//   2. Enable the collider  →  hit window opens
//   3. Unsubscribe and disable the collider  →  hit window closes
//   4. HitSet clears itself in OnDisable — nothing to clean up manually
//
// Inspector setup:
//   - Add this component to the hitbox child GameObject
//   - Set the Collider on the same GameObject to isTrigger = true
//   - Assign the correct Layer so it only overlaps with valid targets
//     (e.g. "Enemy" layer for Guardian hitboxes, "Player" layer for CuBot hitboxes)

using System;
using System.Collections.Generic;
using UnityEngine;

public class Mb_HitboxZone : MonoBehaviour
{
    // Fired when a new MB_CuBotBase enters the trigger this activation.
    // Passes the CuBot so the ability can deal damage, apply modifiers, etc.
    // For CuBot hitboxes hitting the Guardian, wire this differently — see note below.
    public event Action<MB_CuBotBase> OnHit;

    // Tracks which enemies have already been hit this activation.
    // Cleared in OnDisable so each enable/disable cycle starts fresh.
    private readonly HashSet<MB_CuBotBase> _hitThisActivation = new HashSet<MB_CuBotBase>();

    private Mb_CharacterBase _owner;


    /// <summary>
    /// Call this once after instantiation so the hitbox knows whose hits to ignore.
    /// Abilities call this in OnEquip, or GuardianBase/CuBotBase can set it in Awake.
    /// </summary>
    public void SetOwner(Mb_CharacterBase owner)
    {
        _owner = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignore the character who owns this hitbox
        if (_owner != null && other.gameObject == _owner.gameObject) return;

        MB_CuBotBase cuBot = other.GetComponent<MB_CuBotBase>();
        if (cuBot == null) return;

        if (_hitThisActivation.Contains(cuBot)) return;

        _hitThisActivation.Add(cuBot);
        OnHit?.Invoke(cuBot);
    }


    private void OnDisable()
    {
        // Clear the hit set so the next activation is clean.
        // Unsubscribing from OnHit is the ability's responsibility —
        // done in the ability's own cleanup so dangling listeners don't accumulate.
        _hitThisActivation.Clear();
    }
}