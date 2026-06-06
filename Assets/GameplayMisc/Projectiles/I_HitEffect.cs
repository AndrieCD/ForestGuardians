// I_HitEffect.cs
// Interface that every on-hit effect must implement.
//
// WHY AN INTERFACE:
//   Mb_Projectile holds a List<Sc_HitEffect> and calls ApplyOnHit() on each one.
//   Because all effects implement this interface, the projectile never needs to
//   know what any specific effect does — it just calls the method and moves on.
//   Adding a new effect type (e.g. burn, freeze) never requires touching Mb_Projectile.
//
// IMPLEMENTORS:
//   Sc_HitEffect (abstract base) → Sc_HitEffect_Damage, Sc_HitEffect_Knockback,
//   Sc_HitEffect_Slow, Sc_HitEffect_Stun

public interface I_HitEffect
{
    /// <summary>
    /// Applies this effect to the target that was hit.
    /// Called by Mb_Projectile for every valid hit.
    /// </summary>
    /// <param name="target">The character that was hit.</param>
    /// <param name="attacker">The character who fired the projectile.</param>
    /// <param name="hitPoint">World-space position of the collision.</param>
    /// <param name="hitNormal">Surface normal at the collision point — used for
    /// knockback direction so the target is pushed away from the impact surface.</param>
    void ApplyOnHit(Mb_CharacterBase target, Mb_CharacterBase attacker,
                    UnityEngine.Vector3 hitPoint, UnityEngine.Vector3 hitNormal);
}