using System.Collections.Generic;
using UnityEngine;

public enum MaterialCategory { Raw, Product, Chip }

[CreateAssetMenu(fileName = "MaterialData", menuName = "Scriptable Objects/MaterialData")]
public class MaterialData : ScriptableObject
{
    [Header("Basic Info")]
    public int id; 
    public string materialName;
    public MaterialType materialType = MaterialType.None;
    public MaterialCategory materialCategory = MaterialCategory.Raw;
    public Sprite icon;

    //public something something recipe

    public string unit = "pcs";
    public int unitScale = 1; // 1 = piece
    public int maxStack = 9999; // soft limit for UI

    //tags for recipes (e.g metal, conductor)
    public List<string> tags = new();

    public override string ToString()
    {
        return $"{materialName} ({materialType}, {materialCategory})";
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            Debug.LogWarning("Material name not valid.");
        }
    }
}
