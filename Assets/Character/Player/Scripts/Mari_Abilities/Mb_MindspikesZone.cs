using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// Mb_MindspikesZone.cs
// Attached to the spike zone prefab. Handles trigger detection, damage ticking,
// slow application, visual scaling, and self-destruction after duration expires.
//
// Kept as a separate class (not an inner class) so Unity can serialize it
// as a prefab component and so Leo can reference it in the Editor.
// =============================================================================


public class Mb_MindspikesZone : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Runtime State (set by Initialize())
    // -------------------------------------------------------------------------

    private Mb_CharacterBase _owner;
    private float _damage;
    private float _tickInterval;
    private float _slowPercent;
    private float _slowDuration;
    private float _duration;
    private bool _isOvercharged;

    // Tracks the last time each enemy was damaged — prevents per-frame ticking
    // Key: CuBot instance, Value: Time.time of last damage tick
    private Dictionary<MB_CuBotBase, float> _tickTimers
        = new Dictionary<MB_CuBotBase, float>();

    // The BoxCollider on this GO — resized to match final dimensions in Initialize()
    private BoxCollider _collider;

    // Looping field VFX child — located by name, plays while zone is active
    private ParticleSystem _fieldVFX;

    // The visual mesh root — scaled to match collider dimensions
    // TODO: Leo — name the spike mesh child GO "SpikeVisualRoot" on the prefab
    private Transform _visualRoot;


    // -------------------------------------------------------------------------
    // Initialize
    // Called by Mari_Q immediately after Instantiate()
    // -------------------------------------------------------------------------

    public void Initialize(
        Mb_CharacterBase owner,
        float damage,
        float tickInterval,
        float slowPercent,
        float slowDuration,
        float length,
        float width,
        float height,
        float duration,
        bool isOvercharged)
    {
        _owner = owner;
        _damage = damage;
        _tickInterval = tickInterval;
        _slowPercent = slowPercent;
        _slowDuration = slowDuration;
        _duration = duration;
        _isOvercharged = isOvercharged;

        // --- Resize BoxCollider to match final resolved dimensions ---
        _collider = GetComponent<BoxCollider>();
        if (_collider != null)
        {
            // BoxCollider.size is in local space — width=X, height=Y, length=Z
            _collider.size = new Vector3(width, height, length);
            // Center is at (0, height/2, 0) so the bottom of the box sits at
            // the GO's origin (which is placed at Mari's foot level)
            _collider.center = new Vector3(0f, height / 2f, 0f);
        }
        else
        {
            Debug.LogError("[Mb_MindspikesZone] No BoxCollider found on spike zone prefab.");
        }

        // --- Scale visual root to match collider ---
        // SpikeVisualRoot is a child GO whose localScale drives the mesh size.
        // We scale it to (width, 1, length) — height is intentionally not scaled
        // so the visual mesh stays flat/ground-level regardless of collider height.
        _visualRoot = transform.Find("SpikeVisualRoot");
        if (_visualRoot != null)
            _visualRoot.localScale = new Vector3(width, 1f, length);

        // --- Locate and play field VFX ---
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "MindspikesFieldVFX")
            {
                _fieldVFX = ps;
                break;
            }
        }
        _fieldVFX?.Play();

        // --- Overcharged visual tint ---
        // TODO: If the spike zone has a Renderer, tint it a different color
        // when overcharged so the player can visually distinguish the empowered field.
        // Suggested: overcharged = brighter purple/white pulse vs. normal blue-green.
        // Implement once Leo finalizes the material setup on the prefab.
        if (isOvercharged)
        {
            // Placeholder: scale the VFX emission rate up for overcharge intensity
            if (_fieldVFX != null)
            {
                var emission = _fieldVFX.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                    emission.rateOverTime.constant * 2f
                );
            }
        }

        // Start the self-destruct timer
        StartCoroutine(DurationRoutine());
    }


    // -------------------------------------------------------------------------
    // Trigger Detection
    // -------------------------------------------------------------------------

    private void OnTriggerStay(Collider other)
    {
        MB_CuBotBase enemy = other.GetComponent<MB_CuBotBase>();
        if (enemy == null) return;
        if (enemy.Health == null || enemy.Health.IsDead) return;

        // Tick gate — only damage this enemy if enough time has passed
        float now = Time.time;
        if (_tickTimers.TryGetValue(enemy, out float lastTick))
        {
            if (now - lastTick < _tickInterval) return;
        }

        _tickTimers[enemy] = now;

        ApplyDamageAndSlow(enemy);
    }

    private void OnTriggerExit(Collider other)
    {
        // Remove from tick tracker when enemy leaves the zone —
        // so if they re-enter they can be damaged immediately rather than
        // waiting out whatever was left of the tick interval
        MB_CuBotBase enemy = other.GetComponent<MB_CuBotBase>();
        if (enemy != null)
            _tickTimers.Remove(enemy);
    }


    // -------------------------------------------------------------------------
    // Damage and Slow Application
    // -------------------------------------------------------------------------

    private void ApplyDamageAndSlow(MB_CuBotBase enemy)
    {
        // --- Damage ---
        enemy.Health.TakeDamage(_damage);

        // --- Slow modifier ---
        // Built fresh each application — timed removal is handled by Mb_StatBlock
        // StatModType.Percent with a negative value reduces the stat:
        // finalMoveSpeed = base * (1 + (-SlowPercent / 100)) = base * (1 - SlowPercent%)
        Sc_StatEffect slowEffect = new Sc_StatEffect(
            StatType.MoveSpeed,
            -_slowPercent,
            StatModType.Percent
        );

        Sc_Modifier slowModifier = new Sc_Modifier(
            "Mindspikes Slow",
            ModifierSource.Ability,
            new System.Collections.Generic.List<Sc_StatEffect> { slowEffect },
            _slowDuration
        );

        enemy.Stats.AddModifier(slowModifier);

        Debug.Log($"[Mb_MindspikesZone] Hit {enemy.CharacterName} for {_damage} " +
                  $"and applied {_slowPercent}% slow.");
    }


    // -------------------------------------------------------------------------
    // Duration and Cleanup
    // -------------------------------------------------------------------------

    private IEnumerator DurationRoutine()
    {
        yield return new WaitForSeconds(_duration);
        DeactivateZone();
    }


    private void DeactivateZone()
    {
        // Stop field VFX before deactivating so particles fade out naturally
        // rather than popping off instantly
        if (_fieldVFX != null)
            _fieldVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Clear tick state
        _tickTimers.Clear();

        // Give the VFX a short window to finish emitting before destroying.
        // TODO: Tune this delay to match the VFX fade-out duration Leo sets.
        StartCoroutine(DestroyAfterDelay(1.5f));
    }


    private IEnumerator DestroyAfterDelay(float delay)
    {
        // Disable the collider immediately so no more damage ticks fire
        // during the VFX fade window
        if (_collider != null)
            _collider.enabled = false;

        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
}