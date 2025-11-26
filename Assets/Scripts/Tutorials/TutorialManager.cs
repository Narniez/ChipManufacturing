using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    [Header("Authoring")]
    [SerializeField] private TutorialSequence sequence;
    [SerializeField] private TutorialOverlayUI overlay;
    [SerializeField, Tooltip("Auto start when this MonoBehaviour enables.")] private bool autoStart = true;

    private int _index = -1;

    private void OnEnable()
    {
        TutorialEventBus.OnShopOpened += Bus_ShopOpened;
        TutorialEventBus.OnShopItemSelected += Bus_ShopItemSelected;
        TutorialEventBus.OnShopBuyClicked += Bus_ShopBuyClicked;
        TutorialEventBus.OnInventoryOpened += Bus_InventoryOpened;
        TutorialEventBus.OnPreviewStarted += Bus_PreviewStarted;
        TutorialEventBus.OnPreviewConfirmed += Bus_PreviewConfirmed;
        TutorialEventBus.OnOccupantPlaced += Bus_OccupantPlaced;

        TutorialEventBus.OnInventoryOpened += Bus_InventoryOpened;
        TutorialEventBus.OnRecipeTreeOpened += Bus_RecipeTreeOpened;
        TutorialEventBus.OnMachineFixClicked += Bus_FixMachineButtonClicked;
        TutorialEventBus.OnMachineBoostClicked += Bus_BoostMachineButtonClicked;
        TutorialEventBus.OnInventoryItemSelected += Bus_InventoryItemSelected;
        TutorialEventBus.OnInventoryItemSold += Bus_InventoryItemSold;

        if (autoStart) StartSequence();
    }

    private void OnDisable()
    {
        TutorialEventBus.OnShopOpened -= Bus_ShopOpened;
        TutorialEventBus.OnShopItemSelected -= Bus_ShopItemSelected;
        TutorialEventBus.OnShopBuyClicked -= Bus_ShopBuyClicked;
        TutorialEventBus.OnPreviewStarted -= Bus_PreviewStarted;
        TutorialEventBus.OnPreviewConfirmed -= Bus_PreviewConfirmed;
        TutorialEventBus.OnOccupantPlaced -= Bus_OccupantPlaced;

        TutorialEventBus.OnInventoryOpened -= Bus_InventoryOpened;
        TutorialEventBus.OnRecipeTreeOpened -= Bus_RecipeTreeOpened;
        TutorialEventBus.OnMachineFixClicked -= Bus_FixMachineButtonClicked;
        TutorialEventBus.OnMachineBoostClicked -= Bus_BoostMachineButtonClicked;
        TutorialEventBus.OnInventoryItemSelected -= Bus_InventoryItemSelected;
        TutorialEventBus.OnInventoryItemSold -= Bus_InventoryItemSold;
    }

    private void Update()
    {
        if (!HasActiveStep) return;
        var step = sequence.steps[_index];
        if (step.waitForSignal == TutorialSignal.None && Input.GetMouseButtonDown(0))
            Advance();
    }

    public void StartSequence()
    {
        _index = -1;
        Advance();
    }

    public void StopSequence()
    {
        _index = -1;
        overlay?.Show(false);
    }

    private void Advance()
    {
        _index++;

        if (sequence == null)
        {
            Debug.LogWarning("TutorialManager: No TutorialSequence assigned.");
            overlay?.Show(false);
            return;
        }
        if (sequence.steps == null || sequence.steps.Count == 0)
        {
            Debug.LogWarning("TutorialManager: TutorialSequence has no steps.");
            overlay?.Show(false);
            return;
        }
        if (_index >= sequence.steps.Count)
        {
            overlay?.Show(false);
            return;
        }

        var step = sequence.steps[_index];
        overlay?.Show(true);

        RectTransform target = ResolveRect(step.highlightTargetPath);
        overlay?.ConfigureStep(step, target);
    }

    private bool HasActiveStep => sequence != null && sequence.steps != null && _index >= 0 && _index < sequence.steps.Count;

    private RectTransform ResolveRect(string pathOrName)
    {
        if (string.IsNullOrEmpty(pathOrName)) return null;
        var go = GameObject.Find(pathOrName);
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    private void TryAdvance(TutorialSignal sig, object payload)
    {
        if (!HasActiveStep) return;

        var step = sequence.steps[_index];
        if (step.waitForSignal != sig) return;

        Advance();
    }

    public void OnClickShop() => TutorialEventBus.PublishShopOpened();
    public void OnShopBuyButton() => TutorialEventBus.PublishShopBuyClicked(null);
    public void OnConfirmButton() => TutorialEventBus.PublishPreviewConfirmed();
    
    public void OnClickInventory() => TutorialEventBus.PublishInventoryOpened();
    public void OnClickRecipeTree() => TutorialEventBus.PublishRecipeTreeOpened();
    public void OnClickInventoryItem() => TutorialEventBus.PublishInventoryItemSelected();
    public void OnClickInventoryItemSell() => TutorialEventBus.PublishInventoryItemSold();
    public void OnClickFixMachineButton() => TutorialEventBus.PublishMachineFixButtonClicked();
    public void OnClickBoostMachineButton() => TutorialEventBus.PublishMachineBoostButtonClicked();


    private void Bus_ShopOpened() => TryAdvance(TutorialSignal.ShopOpened, null);
    private void Bus_ShopItemSelected(MachineData data) => TryAdvance(TutorialSignal.ShopItemSelected, data);
    private void Bus_ShopBuyClicked(MachineData data) => TryAdvance(TutorialSignal.ShopBuyClicked, data);
    private void Bus_PreviewStarted(GameObject prefab) => TryAdvance(TutorialSignal.PreviewStarted, prefab);
    private void Bus_PreviewConfirmed() => TryAdvance(TutorialSignal.PreviewConfirmed, null);
    private void Bus_OccupantPlaced(IGridOccupant occ) => TryAdvance(TutorialSignal.MachinePlaced, occ);

    private void Bus_InventoryOpened() => TryAdvance(TutorialSignal.InventoryOpened, null);
    private void Bus_RecipeTreeOpened() => TryAdvance(TutorialSignal.RecipeTreeOpened, null);
    private void Bus_InventoryItemSelected() => TryAdvance(TutorialSignal.InventoryItemSelected, null);
    private void Bus_InventoryItemSold() => TryAdvance(TutorialSignal.InventoryItemSold, null);
    private void Bus_FixMachineButtonClicked() => TryAdvance(TutorialSignal.ClickFixMachineButton, null);
    private void Bus_BoostMachineButtonClicked() => TryAdvance(TutorialSignal.ClickBoostMachineButton, null);
}