// Mb_FootstepVFXEmitter.cs
// Spawns footstep VFX when the Guardian's foot contacts the ground.
// Attach this to the same GameObject as the Guardian's Animator.
//
// HOW FOOTSTEP VFX ARE TRIGGERED:
//   Animation events fire from the Guardian's walk/run animation clips at the
//   exact frame where each foot contacts the ground. Those events call methods
//   on Mb_GuardianAnimator, which then calls TriggerLeftFootstep() or
//   TriggerRightFootstep() on this component.
//
//   WHY ANIMATION EVENTS INSTEAD OF RAYCASTING EVERY FRAME:
//   Animation events tie the visual effect to the exact animation frame of foot
//   contact — this is what makes footsteps feel correct. Raycasting every frame
//   is wasteful, and trigger colliders on terrain are fragile across multiple
//   terrain types. Animation events cost nothing between footsteps.
//
// HOW SURFACE DETECTION WORKS:
//   When a footstep event fires, a short downward raycast is fired from the
//   foot bone position. The hit layer is matched against the SurfaceMappings
//   list in the Inspector to find the correct VFXType. If no match is found,
//   Footstep_Stone is used as a fallback.
//
// ANIMATOR SETUP REQUIRED:
//   In each walk/run animation clip, add two Animation Events:
//     - At the left foot contact frame:  call "OnLeftFootContact"
//     - At the right foot contact frame: call "OnRightFootContact"
//   These method names must match exactly — Unity calls them by string on the
//   Animator's GameObject. They are defined at the bottom of this script.
//
// INSPECTOR SETUP:
//   - Left Foot Bone:    drag the left foot bone Transform from the rig
//   - Right Foot Bone:   drag the right foot bone Transform from the rig
//   - Surface Mappings:  add one entry per terrain layer
//       Layer Mask: select the physics layer (e.g. "Grass", "Stone")
//       VFX Type:   pick the matching footstep VFXType enum value
//   - Raycast Distance:  default 0.3f — short ray, only needs to reach the ground
//   - Raycast Layer Mask: set to your terrain/ground layers only — avoids hitting
//                         character colliders or projectiles
//
// TODO: Once terrain layers are finalized in the project, configure SurfaceMappings:
//   Layer "Grass"  → VFXType.Footstep_Grass
//   Layer "Water"  → VFXType.Footstep_Water
//   Layer "Mud"    → VFXType.Footstep_Mud
//   Layer "Stone"  → VFXType.Footstep_Stone  (also used as fallback)

using System;
using System.Collections.Generic;
using UnityEngine;

public class Mb_FootstepVFXEmitter : MonoBehaviour
{
    #region Inspector Fields        //----------------------------------------

    [Header("Foot Bone References")]
    [Tooltip("Drag the left foot bone Transform from the character rig here. " +
             "The footstep raycast fires downward from this position.")]
    [SerializeField] private Transform _LeftFootBone;

    [Tooltip("Drag the right foot bone Transform from the character rig here.")]
    [SerializeField] private Transform _RightFootBone;

    [Header("Surface Detection")]
    [Tooltip("How far downward the ray is cast from the foot bone to detect the surface. " +
             "0.3f is enough to reach the ground from a foot bone — increase if the rig " +
             "has the foot bone positioned higher above the mesh.")]
    [SerializeField] private float _RaycastDistance = 0.3f;

    [Tooltip("Physics layers the footstep ray should hit. " +
             "Set this to your terrain and ground layers ONLY — exclude Character, " +
             "Projectile, and any other non-ground layers to avoid false hits.")]
    [SerializeField] private LayerMask _GroundLayerMask;

    [Tooltip("One entry per terrain surface type. " +
             "The ray's hit layer is checked against each entry in order — first match wins. " +
             "If no entry matches, Footstep_Stone is used as a fallback.")]
    [SerializeField] private List<SurfaceVFXMapping> _SurfaceMappings = new List<SurfaceVFXMapping>();

    #endregion                      //----------------------------------------


    #region Private State           //----------------------------------------

    // Cached list of layer indices extracted from each mapping's LayerMask.
    // Built once in Awake so the footstep handler doesn't do bit math every step.
    // Index matches _SurfaceMappings — _layerIndices[i] is the layer for _SurfaceMappings[i].
    private List<int> _layerIndices = new List<int>();

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        // Validate foot bone references — missing bones mean no footstep VFX,
        // which is easy to miss during development without a clear error
        if (_LeftFootBone == null)
            Debug.LogWarning($"[Mb_FootstepVFXEmitter] Left Foot Bone is not assigned on " +
                             $"{gameObject.name}. Left footstep VFX will not play.");

        if (_RightFootBone == null)
            Debug.LogWarning($"[Mb_FootstepVFXEmitter] Right Foot Bone is not assigned on " +
                             $"{gameObject.name}. Right footstep VFX will not play.");

        BuildLayerIndexCache();
    }

    #endregion                      //----------------------------------------


    #region Animation Event Receivers //----------------------------------------
    // These methods are called by Animation Events on the walk/run clips.
    // Unity calls them by string match on this GameObject's components —
    // the method names must match the Animation Event function names exactly.
    // Route through Mb_GuardianAnimator if you want a single point of control,
    // or call these directly from Animation Events on the Animator component.

    /// <summary>
    /// Called by the Animation Event on the left foot contact frame.
    /// Add an Animation Event named "OnLeftFootContact" in the walk/run clip.
    /// </summary>
    public void OnLeftFootContact()
    {
        SpawnFootstepAt(_LeftFootBone);
    }


    /// <summary>
    /// Called by the Animation Event on the right foot contact frame.
    /// Add an Animation Event named "OnRightFootContact" in the walk/run clip.
    /// </summary>
    public void OnRightFootContact()
    {
        SpawnFootstepAt(_RightFootBone);
    }

    #endregion                      //----------------------------------------


    #region Core Logic              //----------------------------------------

    // Fires a downward ray from the foot bone, detects the surface layer,
    // maps it to a VFXType, and tells the VFXManager to play it.
    // Called only from animation events — never per-frame.
    private void SpawnFootstepAt(Transform footBone)
    {
        // Guard: if the bone reference is missing, silently skip —
        // the warning in Awake already told the developer about this
        if (footBone == null) return;

        // Fire the ray straight down from the foot bone position.
        // Short distance (default 0.3f) — we only need to reach the ground surface,
        // not detect anything further below.
        Ray ray = new Ray(footBone.position, Vector3.down);

        if (!Physics.Raycast(ray, out RaycastHit hit, _RaycastDistance, _GroundLayerMask))
        {
            // Ray missed — Guardian might be airborne or just barely off the ground.
            // Silently skip: no footstep VFX mid-air.
            return;
        }

        // Match the hit layer against our surface mappings
        VFXType footstepType = GetFootstepTypeForLayer(hit.collider.gameObject.layer);

        // Orient the VFX to align with the surface normal so dust/splash
        // particles emit perpendicular to the ground, not straight up
        Quaternion surfaceRotation = Quaternion.LookRotation(hit.normal);

        // Spawn at the exact hit point — on the surface, not at the foot bone height
        Mb_VFXManager.Play(footstepType, hit.point, surfaceRotation);
    }


    // Looks up the VFXType for a given physics layer index.
    // Iterates the cached layer index list — first match wins.
    // Falls back to Footstep_Stone if nothing matches.
    private VFXType GetFootstepTypeForLayer(int layer)
    {
        for (int i = 0; i < _layerIndices.Count; i++)
        {
            // _layerIndices[i] holds the single extracted layer for mapping i.
            // -1 means the mapping had an invalid or empty LayerMask — skip it.
            if (_layerIndices[i] == -1) continue;

            if (_layerIndices[i] == layer)
                return _SurfaceMappings[i].FootstepVFX;
        }

        // No match found — fall back to stone/default.
        // TODO: If the layer system is not yet configured, all footsteps will
        //       use this fallback. That is intentional for prototype phase.
        return VFXType.Footstep_Stone;
    }


    // Extracts a single layer index from each mapping's LayerMask and caches it.
    // LayerMask is a bitmask — we extract the first set bit as the layer index.
    // This means each SurfaceVFXMapping should map to exactly ONE layer.
    // If a multi-layer mask is assigned, only the lowest layer bit is used
    // and a warning is logged.
    private void BuildLayerIndexCache()
    {
        _layerIndices.Clear();

        for (int i = 0; i < _SurfaceMappings.Count; i++)
        {
            int maskValue = _SurfaceMappings[i].SurfaceLayer.value;

            if (maskValue == 0)
            {
                // Empty mask — no layer selected in the Inspector for this entry
                Debug.LogWarning($"[Mb_FootstepVFXEmitter] SurfaceMappings[{i}] has no layer " +
                                 $"selected. This entry will never match. " +
                                 $"Assign a layer in the Inspector.");
                _layerIndices.Add(-1);
                continue;
            }

            // Extract the index of the lowest set bit in the mask.
            // e.g. mask 0b00001000 → layer index 3
            int layerIndex = 0;
            int temp = maskValue;

            // Shift right until we find the first set bit — that is the layer index
            while ((temp & 1) == 0)
            {
                temp >>= 1;
                layerIndex++;
            }

            // Warn if more than one bit is set — multi-layer masks are not supported here
            // because Physics.Raycast returns a single hit layer, not a mask
            if ((maskValue & (maskValue - 1)) != 0)
            {
                Debug.LogWarning($"[Mb_FootstepVFXEmitter] SurfaceMappings[{i}] has multiple " +
                                 $"layers selected. Only layer index {layerIndex} will be used. " +
                                 $"Use one layer per mapping entry.");
            }

            _layerIndices.Add(layerIndex);
        }
    }

    #endregion                      //----------------------------------------
}


// ─────────────────────────────────────────────────────────────────────────────
// Supporting Data Type
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One row in the SurfaceMappings list — pairs a physics layer with a footstep VFXType.
/// Add one entry per terrain surface type in the Inspector.
/// </summary>
[Serializable]
public class SurfaceVFXMapping
{
    [Tooltip("The physics layer that represents this surface type. " +
             "Select exactly ONE layer — multi-layer selection is not supported.")]
    public LayerMask SurfaceLayer;

    [Tooltip("The VFX to play when the Guardian steps on this surface type. " +
             "Pick from the Footstep_* entries in VFXType.")]
    public VFXType FootstepVFX;
}