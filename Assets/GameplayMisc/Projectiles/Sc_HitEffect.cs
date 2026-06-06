// Sc_HitEffect.cs
// Abstract base class for all on-hit effect ScriptableObjects.
//
// WHY A SCRIPTABLEOBJECT BASE:
//   Making this a ScriptableObject means each effect type can be created as a
//   project asset and assigned directly in the SO_ProjectileData Inspector list.
//   No code changes needed to mix and match effects on a projectile — just drag
//   in the assets you want. Each asset is shared and read-only at runtime, so
//   there is no state stored here — all effect logic reads from serialized fields
//   and writes only to the target character's components.
//
// HOW TO ADD A NEW EFFECT TYPE:
//   1. Create a new class that inherits from Sc_HitEffect
//   2. Add [CreateAssetMenu] so it can be created as a project asset
//   3. Override ApplyOnHit() with the effect logic
//   4. Create the asset and add it to SO_ProjectileData.HitEffects in the Inspector

using UnityEngine;

public abstract class Sc_HitEffect : ScriptableObject, I_HitEffect
{
    /// <summary>
    /// Override in each derived class to define what happens when this
    /// effect is applied to a hit target.
    /// </summary>
    public abstract void ApplyOnHit(Mb_CharacterBase target, Mb_CharacterBase attacker,
                                    Vector3 hitPoint, Vector3 hitNormal);
}