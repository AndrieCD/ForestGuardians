// Mb_MinimapIcon.cs
// Attach this to the child GameObject (Minimap layer) on any object
// that needs a tracked icon on the minimap: Guardian, CuBots, Panoharra, portals.
//
// HOW IT WORKS:
//   - On OnEnable, registers itself with Mb_MinimapIconRegistrar singleton.
//   - On OnDisable, unregisters — pool-safe for CuBots.
//   - The SpriteRenderer on this same GameObject IS the icon.
//     The minimap camera sees it directly — no extra UI positioning needed.
//   - For the Guardian icon specifically, this script rotates the child GO
//     each frame to match the Guardian's Y rotation so the arrow points correctly.
//
// Inspector Setup:
//   - IconType: set per prefab (Guardian, CuBot, Panoharra, Portal, Water)
//   - IsDirectional: tick only for the Guardian — enables Y-rotation tracking
//   - OwnerTransform: drag the ROOT transform of the owning object (not this child GO).
//     This is what the rotation tracks. Auto-found via transform.parent if left null.

using UnityEngine;

public class Mb_MinimapIcon : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Tooltip("What kind of entity this icon represents. Used by the registrar for filtering.")]
    public MinimapIconType IconType = MinimapIconType.CuBot;

    [Tooltip("Tick for the Guardian only. Rotates this icon GO to match the owner's Y rotation.")]
    [SerializeField] private bool IsDirectional = false;

    [Tooltip("The root transform to track rotation from. Auto-set to transform.parent if null.")]
    [SerializeField] private Transform OwnerTransform;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        // Auto-find owner if not assigned — the parent is always the owning character root
        if (OwnerTransform == null)
            OwnerTransform = transform.parent;
    }


    private void OnEnable()
    {
        Mb_MinimapIconRegistrar.Register(this);
    }


    private void OnDisable()
    {
        Mb_MinimapIconRegistrar.Unregister(this);
    }


    //private void LateUpdate()
    //{
    //    // Only the Guardian arrow needs directional rotation.
    //    // CuBot dots and Panoharra icons are non-directional — no rotation needed.
    //    if (!IsDirectional || OwnerTransform == null) return;

    //    // Rotate ONLY on the Z axis in world space to match the owner's Y (yaw).
    //    // The minimap camera looks straight down, so the icon's local Z rotation
    //    // maps to left/right orientation on the minimap as seen from above.
    //    // We negate because screen-space rotation is mirrored vs world-space yaw.
    //    float yaw = OwnerTransform.eulerAngles.y;
    //    transform.rotation = Quaternion.Euler(90f, 0f, -yaw);
    //    // NOTE: The 90f on X is because SpriteRenderer icons face up (+Y) by default
    //    // when placed as children of a world-space GO. Adjust if your icon prefab
    //    // is oriented differently.
    //    // TODO: Verify rotation offset in-editor — the exact Euler values depend on
    //    //       how your icon child GO was created. If the arrow points wrong,
    //    //       try Quaternion.Euler(90f, 0f, yaw) or (90f, yaw, 0f).
    //}

    #endregion                  //----------------------------------------
}


// -------------------------------------------------------------------------
// Supporting Enum
// -------------------------------------------------------------------------

public enum MinimapIconType
{
    Guardian,
    CuBot,
    Panoharra,
    Portal,
    Water       // extensible — add more as needed
}