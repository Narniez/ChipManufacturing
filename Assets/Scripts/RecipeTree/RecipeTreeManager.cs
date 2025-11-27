using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class RecipeTreeManager : MonoBehaviour
{
    [Tooltip("All recipe trees in the game")]
    [SerializeField] private List<RecipeItemSO> recipeTrees = new();

    [Tooltip("Default locked sprite for recipe nodes")]
    [SerializeField] private Sprite globalLockedSprite;

    // Track which materials have EVER been produced
    private HashSet<MaterialData> unlockedMaterials = new();

    // Track which final products have been produced
    private HashSet<RecipeItemSO> unlockedProducts = new();

    // Events
    public System.Action<MaterialData> OnMaterialUnlocked;
    public System.Action<RecipeItemSO> OnProductUnlocked;

    private InventoryService inventoryService;

    private void OnEnable()
    {
        inventoryService = InventoryService.Instance;
        if (inventoryService != null)
        {
            inventoryService.OnChanged += OnInventoryChanged;
        }

        InitializeStartingMaterials();
    }

    private void OnDisable()
    {
        if (inventoryService != null)
        {
            inventoryService.OnChanged -= OnInventoryChanged;
        }
    }

    /// <summary>
    /// Initialize materials marked as "starting unlocked" in each recipe tree
    /// </summary>
    private void InitializeStartingMaterials()
    {
        unlockedMaterials.Clear();
        unlockedProducts.Clear();

        foreach (var tree in recipeTrees)
        {
            if (tree?.startingUnlockedMaterials != null)
            {
                foreach (var mat in tree.startingUnlockedMaterials)
                {
                    if (mat != null && !unlockedMaterials.Contains(mat))
                    {
                        UnlockMaterial(mat, raiseEvent: false);
                    }
                }
            }
        }

        Debug.Log($"[RecipeTreeManager] Initialized with {unlockedMaterials.Count} unlocked materials.");
    }

    /// <summary>
    /// Called when inventory changes. Checks if new materials were produced and unlocks them.
    /// </summary>
    private void OnInventoryChanged(IDictionary<int, int> delta, IReadOnlyDictionary<int, int> allCounts)
    {
        if (delta == null) return;

        foreach (var change in delta)
        {
            int materialId = change.Key;
            int quantityDelta = change.Value;

            // Only care about additions (production)
            if (quantityDelta > 0)
            {
                // Find the MaterialData with this ID
                var material = FindMaterialById(materialId);
                if (material != null && !unlockedMaterials.Contains(material))
                {
                    UnlockMaterial(material, raiseEvent: true);

                    // Check if any final products can now be unlocked
                    CheckForUnlockedProducts();
                }
            }
        }
    }

    /// <summary>
    /// Unlock a material globally
    /// </summary>
    private void UnlockMaterial(MaterialData material, bool raiseEvent = true)
    {
        if (material == null || unlockedMaterials.Contains(material))
            return;

        unlockedMaterials.Add(material);

        if (raiseEvent)
        {
            Debug.Log($"[RecipeTreeManager] Material unlocked: {material.materialName}");
            OnMaterialUnlocked?.Invoke(material);
        }
    }

    /// <summary>
    /// Check if any final products can now be unlocked (all tree steps unlocked)
    /// </summary>
    private void CheckForUnlockedProducts()
    {
        foreach (var recipe in recipeTrees)
        {
            if (recipe == null || unlockedProducts.Contains(recipe))
                continue;

            // Check if all tree steps are unlocked
            if (recipe.treeSteps != null && recipe.treeSteps.Count > 0)
            {
                bool allStepsUnlocked = recipe.treeSteps.All(step => step != null && unlockedMaterials.Contains(step));

                if (allStepsUnlocked)
                {
                    UnlockProduct(recipe, raiseEvent: true);
                }
            }
        }
    }

    /// <summary>
    /// Unlock a final product
    /// </summary>
    private void UnlockProduct(RecipeItemSO product, bool raiseEvent = true)
    {
        if (product == null || unlockedProducts.Contains(product))
            return;

        unlockedProducts.Add(product);

        if (raiseEvent)
        {
            Debug.Log($"[RecipeTreeManager] Product unlocked: {product.product?.materialName ?? "Unknown"}");
            OnProductUnlocked?.Invoke(product);
        }
    }

    /// <summary>
    /// Check if a material is unlocked
    /// </summary>
    public bool IsMaterialUnlocked(MaterialData material)
    {
        return material != null && unlockedMaterials.Contains(material);
    }

    /// <summary>
    /// Check if a product is unlocked
    /// </summary>
    public bool IsProductUnlocked(RecipeItemSO product)
    {
        return product != null && unlockedProducts.Contains(product);
    }

    /// <summary>
    /// Get the lock state for displaying a material in a tree
    /// </summary>
    public LockState GetMaterialLockState(MaterialData material)
    {
        if (material == null)
            return LockState.Hidden;

        return IsMaterialUnlocked(material) ? LockState.Unlocked : LockState.Locked;
    }

    /// <summary>
    /// Get the sprite for a material node
    /// </summary>
    public Sprite GetMaterialSprite(RecipeItemSO tree, MaterialData material, LockState lockState)
    {
        if (material == null)
            return null;

        if (lockState == LockState.Locked)
            return globalLockedSprite;

        // For unlocked materials, use the material's own icon (not the tree's icon)
        return material.icon;
    }

    /// <summary>
    /// Get all node data for a recipe tree
    /// </summary>
    public List<(MaterialData material, LockState lockState, Sprite sprite)> GetTreeNodeData(RecipeItemSO tree)
    {
        var nodeData = new List<(MaterialData, LockState, Sprite)>();

        if (tree?.treeSteps == null || tree.treeSteps.Count == 0)
            return nodeData;

        foreach (var material in tree.treeSteps)
        {
            if (material == null)
                continue;

            var lockState = GetMaterialLockState(material);
            var sprite = GetMaterialSprite(tree, material, lockState);

            nodeData.Add((material, lockState, sprite));
        }

        return nodeData;
    }

    /// <summary>
    /// Get progress for a specific tree
    /// </summary>
    public (int unlockedCount, int totalCount) GetTreeProgress(RecipeItemSO tree)
    {
        if (tree?.treeSteps == null || tree.treeSteps.Count == 0)
            return (0, 0);

        int unlocked = tree.treeSteps.Count(mat => mat != null && IsMaterialUnlocked(mat));
        return (unlocked, tree.treeSteps.Count);
    }

    /// <summary>
    /// Get overall progress across all trees
    /// </summary>
    public (int unlockedMaterials, int totalMaterials, int unlockedProducts, int totalProducts) GetGlobalProgress()
    {
        int totalMaterials = recipeTrees
            .Where(t => t?.treeSteps != null)
            .SelectMany(t => t.treeSteps)
            .Where(m => m != null)
            .Distinct()
            .Count();

        return (unlockedMaterials.Count, totalMaterials, unlockedProducts.Count, recipeTrees.Count);
    }

    /// <summary>
    /// Assign MaterialData to child node displays for a recipe tree
    /// </summary>
    public void AssignNodesToTree(RecipeItemSO recipeTree, RecipeTreeNodeDisplay[] nodes)
    {
        if (recipeTree == null)
        {
            Debug.LogError("[RecipeTreeManager] Cannot assign nodes to null recipe tree", this);
            return;
        }

        if (recipeTree.treeSteps == null || recipeTree.treeSteps.Count == 0)
        {
            Debug.LogWarning($"[RecipeTreeManager] Recipe tree '{recipeTree.product?.materialName}' has no tree steps", this);
            return;
        }

        if (nodes == null || nodes.Length == 0)
        {
            Debug.LogWarning($"[RecipeTreeManager] No nodes provided to assign for recipe tree '{recipeTree.product?.materialName}'", this);
            return;
        }

        if (nodes.Length != recipeTree.treeSteps.Count)
        {
            Debug.LogWarning(
                $"[RecipeTreeManager] Node count ({nodes.Length}) doesn't match tree steps ({recipeTree.treeSteps.Count}). " +
                $"Only first {Mathf.Min(nodes.Length, recipeTree.treeSteps.Count)} will be assigned.",
                this);
        }

        // Assign each node a material from the tree steps
        for (int i = 0; i < nodes.Length && i < recipeTree.treeSteps.Count; i++)
        {
            var nodeDisplay = nodes[i];
            var material = recipeTree.treeSteps[i];

            if (nodeDisplay != null)
            {
                nodeDisplay.Initialize(material, recipeTree, this);
            }
        }

        //Debug.Log($"[RecipeTreeManager] Assigned {Mathf.Min(nodes.Length, recipeTree.treeSteps.Count)} nodes for recipe '{recipeTree.product?.materialName}'");
    }

    /// <summary>
    /// Find a MaterialData by ID
    /// </summary>
    private MaterialData FindMaterialById(int id)
    {
        // Search all recipe trees
        foreach (var tree in recipeTrees)
        {
            if (tree?.treeSteps != null)
            {
                var mat = tree.treeSteps.FirstOrDefault(m => m != null && m.id == id);
                if (mat != null)
                    return mat;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the global locked sprite
    /// </summary>
    public Sprite GetGlobalLockedSprite()
    {
        return globalLockedSprite;
    }

    // Editor access
#if UNITY_EDITOR
    public List<RecipeItemSO> AllRecipeTreesForEditing => recipeTrees;
    public HashSet<MaterialData> UnlockedMaterialsForEditing => unlockedMaterials;
    public HashSet<RecipeItemSO> UnlockedProductsForEditing => unlockedProducts;
    public Sprite GlobalLockedSpriteForEditing => globalLockedSprite;

    public void SetAllRecipeTrees(List<RecipeItemSO> trees) => recipeTrees = trees;
    public void SetGlobalLockedSprite(Sprite sprite) => globalLockedSprite = sprite;
#endif
}
