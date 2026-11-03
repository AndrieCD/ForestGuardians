using System.Collections;
using UnityEngine;

/// <summary>
/// Provides the shared exit transition used by Mari's spawned ability VFX.
/// Gameplay components disable their colliders before starting this transition.
/// </summary>
public static class Sc_VFXExitTransition
{
    /// <summary>
    /// Smoothly scales a VFX root to zero, then destroys it.
    /// </summary>
    public static IEnumerator ScaleDownAndDestroy(Transform target, float duration)
    {
        if (target == null) yield break;

        Vector3 initialScale = target.localScale;
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);
            target.localScale = Vector3.LerpUnclamped(
                initialScale,
                Vector3.zero,
                easedProgress
            );

            yield return null;
        }

        if (target != null)
            Object.Destroy(target.gameObject);
    }
}
