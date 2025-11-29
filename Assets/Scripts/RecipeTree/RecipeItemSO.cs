using System.Collections.Generic;
using UnityEngine;

public enum LockState { Unlocked, Locked, Hidden }

[CreateAssetMenu(fileName = "RecipeItem", menuName = "Scriptable Objects/RecipeItem")]
public class RecipeItemSO : ScriptableObject
{
    [Header("Item Info")]
    public MaterialData product;
    public Sprite unlockedIcon;

    [Header("Ingredients tree")]
    [Tooltip("All materials needed in this recipe tree. These nodes can be shared among different products.")]
    public List<MaterialData> treeSteps = new();

    [Header("Starting Materials (Pre-Unlocked)")]
    [Tooltip("Materials that start unlocked for this tree")]
    public List<MaterialData> startingUnlockedMaterials = new List<MaterialData>();
    public override string ToString() => $"Recipe: {product?.materialName ?? "Unknown"}";

    private void OnValidate()
    {
        if(product == null)
        {
            Debug.LogWarning("Product material not assigned.");
        }
        if(treeSteps == null || treeSteps.Count == 0)
        {
            Debug.LogWarning("Tree steps not assigned.");
        }

        for(int i= 0; i < treeSteps.Count; i++)
        {
            if(treeSteps[i] == null)
            {
                Debug.LogWarning($"Tree step at index {i} is not assigned.");
            }
        }
    }
}
