using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Container for a recipe tree in the scene.
/// Manages both the product display and child nodes.
public class RecipeTreeContainer : MonoBehaviour
{
    [SerializeField] private RecipeItemSO recipeTree;
    [SerializeField] private RecipeTreeNodeDisplay[] nodes;
    [SerializeField] private bool autoFindChildNotes = true;

    private Image productIcon;
    private CanvasGroup productCanvasGroup;
    private RecipeTreeManager treeManager;

    private void Awake()
    {
        productIcon = GetComponent<Image>();
        productCanvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        treeManager = FindFirstObjectByType<RecipeTreeManager>();
        if (treeManager == null)
        {
            Debug.LogError("[RecipeTreeContainer] No RecipeTreeManager found in scene", this);
            return;
        }

        // initializing product display
        InitializeProductDisplay();

        // initializing child nodes
        AutoFindChildNodes();
        treeManager.AssignNodesToTree(recipeTree, nodes);
    }

    private void InitializeProductDisplay()
    {
        if (productIcon == null)
            return;

        // subscribing to unlock events
        treeManager.OnMaterialUnlocked += OnMaterialUnlockedGlobal;
        treeManager.OnProductUnlocked += OnProductUnlockedGlobal;

        // setting initial product display
        RefreshProductDisplay();
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
        // refreshing whenever ANY material unlocks (might complete this product)
        RefreshProductDisplay();
    }

    private void OnProductUnlockedGlobal(RecipeItemSO product)
    {
        if (product == recipeTree)
            RefreshProductDisplay();
    }

    private void RefreshProductDisplay()
    {
        if (recipeTree == null || treeManager == null || productIcon == null)
            return;

        bool isUnlocked = treeManager.IsProductUnlocked(recipeTree);

        // showing unlocked icon if product is unlocked, otherwise show locked sprite
        Sprite sprite = isUnlocked ? recipeTree.unlockedIcon : treeManager.GetGlobalLockedSprite();
        productIcon.sprite = sprite;

        // updating visuals
        if (productCanvasGroup != null)
        {
            productCanvasGroup.alpha = isUnlocked ? 1f : 0.5f;
        }
    }

    public void AutoFindChildNodes()
    {
        if (!autoFindChildNotes)
            return;

        var all = GetComponentsInChildren<RecipeTreeNodeDisplay>(includeInactive: false);
        var filtered = new List<RecipeTreeNodeDisplay>(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            var node = all[i];
            if (node == null)
                continue;

            // skip self if a node is placed on the same GameObject as this container
            if (node.transform == transform)
                continue;

            // include only nodes whose nearest parent container is THIS container
            var owningContainer = node.GetComponentInParent<RecipeTreeContainer>();
            if (owningContainer == this)
                filtered.Add(node);
        }

        nodes = filtered.ToArray();
        Debug.Log($"[RecipeTreeContainer] Found {nodes.Length} child nodes for product {gameObject.name}");
    }
}
