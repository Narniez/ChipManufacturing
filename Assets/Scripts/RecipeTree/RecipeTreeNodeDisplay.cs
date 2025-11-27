using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Display layer for recipe tree nodes.
/// Receives MaterialData and RecipeItemSO from RecipeTreeManager.
/// Only handles visuals - no logic.
/// </summary>
public class RecipeTreeNodeDisplay : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private CanvasGroup canvasGroup;

    private MaterialData material;
    private RecipeItemSO recipeTree;
    private RecipeTreeManager treeManager;

    /// <summary>
    /// Called by RecipeTreeManager to initialize this node
    /// </summary>
    public void Initialize(MaterialData materialData, RecipeItemSO recipeTreeSO, RecipeTreeManager manager)
    {
        material = materialData;
        recipeTree = recipeTreeSO;
        treeManager = manager;

        if (treeManager != null)
        {
            treeManager.OnMaterialUnlocked += OnMaterialUnlockedGlobal;
            treeManager.OnProductUnlocked += OnProductUnlockedGlobal;
        }

        // Set initial sprite based on lock state
        Refresh();
    }

    private void OnDestroy()
    {
        if (treeManager != null)
        {
            treeManager.OnMaterialUnlocked -= OnMaterialUnlockedGlobal;
            treeManager.OnProductUnlocked -= OnProductUnlockedGlobal;
        }
    }

    private void OnMaterialUnlockedGlobal(MaterialData unlockedMaterial)
    {
        if (unlockedMaterial == material)
            Refresh();
    }

    private void OnProductUnlockedGlobal(RecipeItemSO product)
    {
        if (product == recipeTree)
            Refresh();
    }

    private void Refresh()
    {
        if (material == null || treeManager == null || icon == null)
            return;

        LockState lockState = treeManager.GetMaterialLockState(material);
        Sprite sprite = treeManager.GetMaterialSprite(recipeTree, material, lockState);

        // Set the sprite on the Image component (not the variable)
        icon.sprite = sprite;

    }
}
