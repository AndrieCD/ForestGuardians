// Mb_FloatingTextPool.cs
// Singleton pool manager for all floating combat text objects in the scene.
// All spawn requests go through here — no floating text is ever instantiated
// at runtime outside of the initial warm-up.
//
// WHY A SINGLETON POOL:
//   Every CuBot in the scene needs to spawn text, but we don't want dozens of
//   separate pools competing for the same prefab instances. One central pool
//   means we can cap the total number of floating text objects in the scene
//   and reuse them across all CuBots without any coordination overhead.
//
// POOL SAFETY:
//   CuBots are reused from their own pool — they are never destroyed and
//   re-instantiated. This pool follows the same pattern: objects are deactivated
//   and re-initialized, never destroyed. Mb_FloatingText.ResetForPool() handles
//   the cleanup before an object is returned to the available stack.
//
// Inspector Setup:
//   - Attach this to a persistent Manager GameObject in the Stage scene.
//   - Assign the FloatingText prefab (must have Mb_FloatingText + TextMeshPro 3D).
//   - Set InitialPoolSize — see the TODO below for a suggested starting value.

using System.Collections.Generic;
using UnityEngine;

public class Mb_FloatingTextPool : MonoBehaviour
{
    public static Mb_FloatingTextPool Instance { get; private set; }

    [Header("Pool Settings")]
    [SerializeField] private GameObject _FloatingTextPrefab;

    // TODO: Tune this based on expected simultaneous hits in the worst-case wave.
    // A wave of 10 Choppers attacking at 1 AS each could fire ~10 texts/sec.
    // 20 is a safe starting point — raise it if you see the pool expanding at runtime.
    [SerializeField] private int _InitialPoolSize = 20;

    // Stack gives us O(1) push and pop — order doesn't matter for a text pool
    private Stack<Mb_FloatingText> _available = new Stack<Mb_FloatingText>();

    // Tracks all instances ever created so we can reset them cleanly on scene teardown
    private List<Mb_FloatingText> _allInstances = new List<Mb_FloatingText>();


    private void Awake()
    {
        // Singleton setup — only one pool should exist per scene
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_FloatingTextPrefab == null)
        {
            Debug.LogError("[Mb_FloatingTextPool] No FloatingText prefab assigned. Pool will not function.");
            return;
        }

        WarmPool();
    }


    /// <summary>
    /// Pre-instantiates all floating text objects at scene start so there are
    /// no allocations during gameplay. All objects start deactivated.
    /// </summary>
    private void WarmPool()
    {
        for (int i = 0; i < _InitialPoolSize; i++)
        {
            Mb_FloatingText instance = CreateNewInstance();
            ReturnToPool(instance);
        }

        Debug.Log($"[Mb_FloatingTextPool] Warmed up with {_InitialPoolSize} instances.");
    }


    /// <summary>
    /// Spawns a damage number above the given world position.
    /// Text size scales linearly between MinFontSize and MaxFontSize based on damage amount.
    /// Pass isCrit = true to use the crit color instead of the default color.
    /// </summary>
    /// <param name="worldPosition">Where to place the text (typically above the CuBot).</param>
    /// <param name="damageAmount">The raw damage value — displayed as a whole number.</param>
    /// <param name="isCrit">If true, uses the crit color defined on the spawner.</param>
    /// <param name="normalColor">Default hit color (white).</param>
    /// <param name="critColor">Crit hit color (yellow/orange).</param>
    /// <param name="minFontSize">Font size at zero damage.</param>
    /// <param name="maxFontSize">Font size at or above the damage cap.</param>
    /// <param name="damageSizeCap">Damage value at which font size reaches its maximum.</param>
    public void SpawnDamageText(
        Vector3 worldPosition,
        float damageAmount,
        bool isCrit,
        Color normalColor,
        Color critColor,
        float minFontSize,
        float maxFontSize,
        float damageSizeCap)
    {
        Mb_FloatingText instance = GetFromPool();

        // t = 0 at 0 damage, t = 1 at damageSizeCap — clamped so it never exceeds max
        float t = Mathf.Clamp01(damageAmount / damageSizeCap);
        float fontSize = Mathf.Lerp(minFontSize, maxFontSize, t);

        Color color = isCrit ? critColor : normalColor;

        // Whole number only — no decimals on combat text
        string text = Mathf.RoundToInt(damageAmount).ToString();

        // Small random horizontal offset so stacked hits from the same source spread out
        Vector3 spawnPos = worldPosition + new Vector3(
            Random.Range(-0.3f, 0.3f),
            0f,
            Random.Range(-0.15f, 0.15f)
        );

        instance.Initialize(spawnPos, text, color, fontSize);
    }


    /// <summary>
    /// Spawns a status label above the given world position.
    /// Status text has a fixed font size — it doesn't scale with any value.
    /// An optional icon sprite can be passed to show alongside the label.
    /// </summary>
    /// <param name="worldPosition">Where to place the text.</param>
    /// <param name="label">Short status label, e.g. "SLOW", "STUN", "POISONED".</param>
    /// <param name="color">The color associated with this status type.</param>
    /// <param name="fontSize">Fixed font size for status text.</param>
    /// <param name="icon">Optional icon sprite shown before the label. Null = no icon.</param>
    public void SpawnStatusText(
        Vector3 worldPosition,
        string label,
        Color color,
        float fontSize,
        Sprite icon = null)
    {
        Mb_FloatingText instance = GetFromPool();

        Vector3 spawnPos = worldPosition + new Vector3(
            Random.Range(-0.2f, 0.2f),
            0f,
            0f
        );

        instance.Initialize(spawnPos, label, color, fontSize, icon);
    }


    // -------------------------------------------------------------------------
    // Pool Internals
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns an available instance from the pool.
    /// If the pool is empty, creates a new instance and logs a warning —
    /// this means InitialPoolSize should be increased.
    /// </summary>
    private Mb_FloatingText GetFromPool()
    {
        if (_available.Count > 0)
        {
            Mb_FloatingText instance = _available.Pop();
            instance.gameObject.SetActive(true);
            return instance;
        }

        // Pool ran dry — expand it rather than returning null, but warn so we can tune the size
        Debug.LogWarning("[Mb_FloatingTextPool] Pool exhausted — creating a new instance. " +
                         "Consider increasing InitialPoolSize.");
        return CreateNewInstance();
    }


    /// <summary>
    /// Resets a floating text instance and pushes it back onto the available stack.
    /// Called automatically via the OnAnimationComplete event on each Mb_FloatingText.
    /// </summary>
    private void ReturnToPool(Mb_FloatingText instance)
    {
        instance.ResetForPool();        // Clears text, alpha, stops coroutines
        _available.Push(instance);
    }


    /// <summary>
    /// Instantiates a new Mb_FloatingText instance, parents it to this manager,
    /// and wires up its OnAnimationComplete callback so it auto-returns to the pool.
    /// </summary>
    private Mb_FloatingText CreateNewInstance()
    {
        GameObject go = Instantiate(_FloatingTextPrefab, transform);
        Mb_FloatingText floatingText = go.GetComponent<Mb_FloatingText>();

        if (floatingText == null)
        {
            Debug.LogError("[Mb_FloatingTextPool] Prefab is missing Mb_FloatingText component.");
            return null;
        }

        // Wire up the return callback — when animation finishes, this instance
        // comes back to the pool automatically without any external coordination
        floatingText.OnAnimationComplete += ReturnToPool;

        _allInstances.Add(floatingText);
        return floatingText;
    }
}