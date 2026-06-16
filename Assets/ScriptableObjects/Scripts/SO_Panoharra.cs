// SO_Panoharra.cs
/// <summary>
/// This ScriptableObject holds the base stats and configuration for the Panoharra Tree.
/// </summary>
using UnityEngine;

[CreateAssetMenu(fileName = "NewPanoharra_SO", menuName = "Panoharra/Panoharra_SO")]
public class SO_Panoharra : ScriptableObject
{
    [Header("General")]
    public string CharacterName;
    //public Sprite icon;
    //public GameObject ModelPrefab;          // The 3D model specific to this guardian

    [Header("Base Stats")]
    public float MaxHealth = 500f;          // Maximum health points
    public float HealthRegen = 5f;            // Health regeneration per second

}