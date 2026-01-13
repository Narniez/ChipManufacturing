using UnityEngine;
using UnityEngine.UI;

// Display layer for recipe tree nodes.
// Receives MaterialData and RecipeItemSO from RecipeTreeManager.
// Only handles visuals - no logic.
public class RecipeTreeNodeDisplay : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private MaterialData material;
    private RecipeItemSO recipeTree;
    private RecipeTreeManager treeManager;

    // Called by RecipeTreeManager to initialize this node
    public void Initialize(MaterialData materialData, RecipeItemSO recipeTreeSO, RecipeTreeManager manager)
    {
        material = materialData;
        recipeTree = recipeTreeSO;
        treeManager = manager;

        if (debugLogging)
        {
            Debug.Log($"[RecipeTreeNodeDisplay] Initialize called for node '{gameObject.name}'" +
                $" | Material: {material?.materialName ?? "null"}" +
                $" | RecipeTree: {recipeTree?.product?.materialName ?? "null"}" +
                $" | Manager: {(manager != null ? "present" : "null")}");
        }

        if (treeManager != null)
        {
            treeManager.OnMaterialUnlocked += OnMaterialUnlockedGlobal;
            treeManager.OnProductUnlocked += OnProductUnlockedGlobal;
            if (debugLogging) Debug.Log($"[RecipeTreeNodeDisplay] Successfully subscribed to manager events");
        }
        else
        {
            if (debugLogging) Debug.LogError($"[RecipeTreeNodeDisplay] treeManager is null! Cannot subscribe to events");
        }

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
        if (debugLogging)
        {
            Debug.Log($"[RecipeTreeNodeDisplay] OnMaterialUnlockedGlobal called for '{unlockedMaterial?.materialName ?? "null"}'");
            Debug.Log($"[RecipeTreeNodeDisplay] My material: '{material?.materialName ?? "null"}' | Match? {unlockedMaterial == material}");
        }

        if (unlockedMaterial == material)
        {
            if (debugLogging) Debug.Log($"[RecipeTreeNodeDisplay] Material matches! Refreshing display...");
            Refresh();
        }
    }

    private void OnProductUnlockedGlobal(RecipeItemSO product)
    {
        if (debugLogging)
        {
            Debug.Log($"[RecipeTreeNodeDisplay] OnProductUnlockedGlobal called for '{product?.product?.materialName ?? "null"}'");
            Debug.Log($"[RecipeTreeNodeDisplay] My recipeTree: '{recipeTree?.product?.materialName ?? "null"}' | Match? {product == recipeTree}");
        }

        if (product != null && recipeTree != null && product.product == recipeTree.product)
        {
            if (debugLogging) Debug.Log($"[RecipeTreeNodeDisplay] Product matches! Refreshing display...");
            Refresh();
        }
    }

    private void Refresh()
    {
        if (material == null)
        {
            if (debugLogging) Debug.LogWarning($"[RecipeTreeNodeDisplay] Refresh called but material is null!");
            return;
        }

        if (treeManager == null)
        {
            if (debugLogging) Debug.LogError($"[RecipeTreeNodeDisplay] Refresh called but treeManager is null!");
            return;
        }

        if (icon == null)
        {
            if (debugLogging) Debug.LogError($"[RecipeTreeNodeDisplay] Refresh called but icon Image component is null!");
            return;
        }

        LockState lockState = treeManager.GetMaterialLockState(material);

        if (debugLogging)
        {
            Debug.Log($"[RecipeTreeNodeDisplay] ========== REFRESH START ==========");
            Debug.Log($"[RecipeTreeNodeDisplay] Material: '{material.materialName}' (ID: {material.id})");
            Debug.Log($"[RecipeTreeNodeDisplay] IsUnlocked: {treeManager.IsMaterialUnlocked(material)}");
            Debug.Log($"[RecipeTreeNodeDisplay] LockState: {lockState}");
            Debug.Log($"[RecipeTreeNodeDisplay] Material.icon: {(material.icon != null ? material.icon.name : "null")}");
        }

        Sprite sprite = treeManager.GetMaterialSprite(recipeTree, material, lockState);

        if (debugLogging)
        {
            Debug.Log($"[RecipeTreeNodeDisplay] GetMaterialSprite returned: {(sprite != null ? sprite.name : "null")}");
            Debug.Log($"[RecipeTreeNodeDisplay] Old icon.sprite: {(icon.sprite != null ? icon.sprite.name : "null")}");
        }

        // setting the sprite on the Image component
        icon.sprite = sprite;

        if (debugLogging)
        {
            Debug.Log($"[RecipeTreeNodeDisplay] After assignment - icon.sprite: {(icon.sprite != null ? icon.sprite.name : "null")}");
            Debug.Log($"[RecipeTreeNodeDisplay] ========== REFRESH END ==========");
        }
    }
}
