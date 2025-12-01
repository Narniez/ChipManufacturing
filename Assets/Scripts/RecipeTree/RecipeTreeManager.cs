using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RecipeTreeManager : MonoBehaviour
{
    [Tooltip("All recipe trees in the game")]
    [SerializeField] private List<RecipeItemSO> recipeTrees = new();

    [Tooltip("Default locked sprite for recipe nodes")]
    [SerializeField] private Sprite globalLockedSprite;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    // Track which materials have EVER been produced
    private HashSet<MaterialData> unlockedMaterials = new();

    // Track which final products have been produced
    private HashSet<RecipeItemSO> unlockedProducts = new();

    // Cache all materials by ID for quick lookup
    private Dictionary<int, MaterialData> materialCache = new();

    // Cache materials by MaterialType for quick lookup
    private Dictionary<MaterialType, MaterialData> materialByTypeCache = new();

    // Events
    public System.Action<MaterialData> OnMaterialUnlocked;
    public System.Action<RecipeItemSO> OnProductUnlocked;

    private InventoryService inventoryService;

    private void OnEnable()
    {
        // Build material cache from all recipes and tree steps
        BuildMaterialCache();

        inventoryService = InventoryService.Instance;
        if (inventoryService != null)
        {
            inventoryService.OnChanged += OnInventoryChanged;
            if (debugLogging) Debug.Log($"[RecipeTreeManager] Subscribed to InventoryService.OnChanged");
        }
        else
        {
            if (debugLogging) Debug.LogError($"[RecipeTreeManager] InventoryService.Instance is NULL on OnEnable!");
        }

        // Subscribe to Machine production events - THIS IS THE KEY!
        Machine.OnMaterialProduced += OnMachineProducedMaterial;
        if (debugLogging) Debug.Log($"[RecipeTreeManager] Subscribed to Machine.OnMaterialProduced");

        InitializeFromStartingMaterials();
    }

    private void OnDisable()
    {
        if (inventoryService != null)
        {
            inventoryService.OnChanged -= OnInventoryChanged;
            if (debugLogging) Debug.Log($"[RecipeTreeManager] Unsubscribed from InventoryService.OnChanged");
        }

        // Unsubscribe from Machine events
        Machine.OnMaterialProduced -= OnMachineProducedMaterial;
        if (debugLogging) Debug.Log($"[RecipeTreeManager] Unsubscribed from Machine.OnMaterialProduced");
    }

    /// <summary>
    /// Build a cache of all materials by ID and MaterialType from tree steps AND final products
    /// This ensures we can find ANY material that appears in the system
    /// </summary>
    private void BuildMaterialCache()
    {
        materialCache.Clear();
        materialByTypeCache.Clear();

        // Add all materials from tree steps
        foreach (var tree in recipeTrees)
        {
            if (tree?.treeSteps != null)
            {
                foreach (var material in tree.treeSteps)
                {
                    if (material != null)
                    {
                        if (!materialCache.ContainsKey(material.id))
                            materialCache[material.id] = material;
                    }
                }
            }
        }

        // IMPORTANT: Also add final products so they can be unlocked when produced
        foreach (var tree in recipeTrees)
        {
            if (tree?.product != null)
            {
                if (!materialCache.ContainsKey(tree.product.id))
                    materialCache[tree.product.id] = tree.product;
            }
        }

        if (debugLogging)
            Debug.Log($"[RecipeTreeManager] Built material cache: {materialCache.Count} by ID, {materialByTypeCache.Count} by type");
    }

    private void OnMachineProducedMaterial(MaterialData material, Vector3 position)
    {
        if (material == null)
        {
            if (debugLogging)
                Debug.LogWarning("[RecipeTreeManager] OnMachineProducedMaterial called with NULL MaterialData");
            return;
        }

        if (debugLogging)
            Debug.Log($"[RecipeTreeManager] ========== Machine produced '{material.materialName}' (ID {material.id}) ==========");

        // 1) Unlock the material itself (for node icons)
        UnlockMaterial(material, raiseEvent: true);

        // 2) Unlock any products whose product is this material
        CheckAndUnlockProductByMaterial(material);

        if (debugLogging)
            Debug.Log($"[RecipeTreeManager] ========== End Machine Production ==========");
    }



    private bool CheckAndUnlockProductByMaterial(MaterialData producedMaterial)
    {
        if (producedMaterial == null)
            return false;

        if (debugLogging)
            Debug.Log($"[RecipeTreeManager] CheckAndUnlockProductByMaterial for '{producedMaterial.materialName}' (ID {producedMaterial.id})");

        bool anyUnlocked = false;

        foreach (var recipe in recipeTrees)
        {
            if (recipe == null || recipe.product == null)
                continue;

            if (unlockedProducts.Contains(recipe))
                continue;

            // Match only by reference or ID (SAFE)
            bool matches =
                recipe.product == producedMaterial ||
                recipe.product.id == producedMaterial.id;

            if (debugLogging)
            {
                Debug.Log($"[RecipeTreeManager]   Checking recipe '{recipe.product.materialName}' " +
                          $"(ID: {recipe.product.id}) vs produced '{producedMaterial.materialName}' -> match? {matches}");
            }

            if (matches)
            {
                UnlockProduct(recipe, raiseEvent: true);
                anyUnlocked = true;
            }
        }

        return anyUnlocked;
    }



    /// <summary>
    /// Initialize only the starting materials. Actual unlocks will happen via production events.
    /// </summary>
    private void InitializeFromStartingMaterials()
    {
        unlockedMaterials.Clear();
        unlockedProducts.Clear();

        // Unlock starting materials marked in recipe trees
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

        // NOTE: Removed CheckForUnlockedProducts() here - products only unlock when produced

        if (debugLogging)
        {
            Debug.Log($"[RecipeTreeManager] Initialized with {unlockedMaterials.Count} starting unlocked materials.");
            if (unlockedMaterials.Count > 0)
                Debug.Log($"[RecipeTreeManager] Starting materials: {string.Join(", ", unlockedMaterials.Select(m => m.materialName))}");
        }
    }

    /// <summary>
    /// Called when inventory changes during runtime (fallback for inventory-only paths)
    /// Most production is now caught by OnMachineProducedMaterial
    /// </summary>
    private void OnInventoryChanged(IDictionary<int, int> delta, IReadOnlyDictionary<int, int> allCounts)
    {
        if (delta == null)
        {
            if (debugLogging) Debug.Log($"[RecipeTreeManager] OnInventoryChanged called with NULL delta (full state sync)");
            return;
        }

        if (debugLogging) Debug.Log($"[RecipeTreeManager] OnInventoryChanged: {delta.Count} items changed (fallback trigger)");

        foreach (var change in delta)
        {
            int materialId = change.Key;
            int quantityDelta = change.Value;

            // Only care about additions
            if (quantityDelta <= 0) continue;

            var material = FindMaterialById(materialId);
            if (material == null) continue;

            if (!unlockedMaterials.Contains(material))
            {
                if (debugLogging) Debug.Log($"[RecipeTreeManager] Unlocking '{material.materialName}' from inventory (fallback)");
                UnlockMaterial(material, raiseEvent: true);
            }
        }
    }

    /// <summary>
    /// Unlock a material globally
    /// </summary>
    private void UnlockMaterial(MaterialData material, bool raiseEvent = true)
    {
        if (material == null || unlockedMaterials.Contains(material))
        {
            if (debugLogging && material != null && unlockedMaterials.Contains(material))
                Debug.Log($"[RecipeTreeManager] Material '{material.materialName}' already unlocked");
            return;
        }

        unlockedMaterials.Add(material);

        if (debugLogging) Debug.Log($"[RecipeTreeManager] MATERIAL UNLOCKED: '{material.materialName}'");

        if (raiseEvent)
        {
            if (debugLogging) Debug.Log($"[RecipeTreeManager] Invoking OnMaterialUnlocked event...");
            OnMaterialUnlocked?.Invoke(material);
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

        if (debugLogging) Debug.Log($"[RecipeTreeManager] PRODUCT UNLOCKED: '{product.product?.materialName ?? "Unknown"}'");

        if (raiseEvent)
        {
            if (debugLogging) Debug.Log($"[RecipeTreeManager] Invoking OnProductUnlocked event...");
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
    }

    /// <summary>
    /// Find a MaterialData by ID using cached lookup
    /// </summary>
    private MaterialData FindMaterialById(int id)
    {
        if (materialCache.TryGetValue(id, out var material))
        {
            return material;
        }

        if (debugLogging)
            Debug.LogWarning($"[RecipeTreeManager] Material ID {id} not found in cache. Cache has {materialCache.Count} materials.");

        return null;
    }

    /// <summary>
    /// Find a MaterialData by MaterialType using cached lookup
    /// </summary>
    private MaterialData FindMaterialByType(MaterialType materialType)
    {
        if (materialByTypeCache.TryGetValue(materialType, out var material))
        {
            return material;
        }

        if (debugLogging)
            Debug.LogWarning($"[RecipeTreeManager] MaterialType {materialType} not found in cache.");

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

