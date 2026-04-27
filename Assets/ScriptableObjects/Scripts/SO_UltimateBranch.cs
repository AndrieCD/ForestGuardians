// SO_UltimateBranch.cs
// Holds the identity data for one Ultimate Branch option —
// its display name, description, and icon for the Rewards Panel UI.
//
// Two of these exist per guardian: one for Branch 1, one for Branch 2.
// Drag both into Mb_RewardsManager's Inspector fields.
//
// Create in Project window:
//   Right-click > Create > ForestGuardians > UltimateBranch

using UnityEngine;

[CreateAssetMenu(fileName = "New UltimateBranch", menuName = "ForestGuardians/UltimateBranch")]
public class SO_UltimateBranch : ScriptableObject
{
    [Header("Identity")]
    public string BranchName;
    [TextArea] public string Description;
    public Sprite Icon;
}