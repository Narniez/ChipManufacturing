using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Container for a recipe tree in the scene.
/// Manages both the product display and child nodes.
/// </summary>
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

        // Initialize product display
        InitializeProductDisplay();

        // Initialize child nodes
        AutoFindChildNodes();
        treeManager.AssignNodesToTree(recipeTree, nodes);
    }

    private void InitializeProductDisplay()
    {
        if (productIcon == null)
            return;

        // Subscribe to unlock events
        treeManager.OnMaterialUnlocked += OnMaterialUnlockedGlobal;
        treeManager.OnProductUnlocked += OnProductUnlockedGlobal;

        // Set initial product display
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
        // Refresh whenever ANY material unlocks (might complete this product)
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

        // Show unlocked icon if product is unlocked, otherwise show locked sprite
        Sprite sprite = isUnlocked ? recipeTree.unlockedIcon : treeManager.GetGlobalLockedSprite();
        productIcon.sprite = sprite;

        // Update visuals
        if (productCanvasGroup != null)
        {
            productCanvasGroup.alpha = isUnlocked ? 1f : 0.5f;
        }
    }

    public void AutoFindChildNodes()
    {
        if (autoFindChildNotes)
        {
            nodes = GetComponentsInChildren<RecipeTreeNodeDisplay>(includeInactive: false);
            Debug.Log($"[RecipeTreeContainer] Found {nodes.Length} child nodes for product {gameObject.name}");
        }
    }
}
