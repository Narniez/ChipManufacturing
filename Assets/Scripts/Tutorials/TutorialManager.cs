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
        TutorialEventBus.OnSignalPublished += OnSignalReceived;
       
    }

    private void OnDisable()
    {
        TutorialEventBus.OnSignalPublished -= OnSignalReceived;
        if (overlay != null) overlay.SkipRequested -= OnSkipRequested;
    }

    private void Start()
    {
        if (overlay != null) overlay.SkipRequested += OnSkipRequested;
        if (autoStart) StartSequence();
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

    private void OnSkipRequested()
    {
        StopSequence();
    }

    private void Advance()
    {
        _index++;

        if (sequence == null || sequence.steps == null || sequence.steps.Count == 0)
        {
            overlay?.Show(false);
            return;
        }
        if (_index >= sequence.steps.Count)
        {
            overlay?.Show(false);
            return;
        }

        PlacementManager.Instance?.HidePortIndicators();
        var step = sequence.steps[_index];
        overlay?.Show(true);

        RectTransform target = ResolveRect(step.highlightTargetPath);
        overlay?.ConfigureStep(step, target);

        if (step.waitForSignal == TutorialSignal.ConveyorConnectedToMachine && !string.IsNullOrEmpty(step.highlightTargetPath))
        {
            var go = GameObject.Find(step.highlightTargetPath);
            var machine = go != null ? go.GetComponent<Machine>() : null;
            if (machine != null)
                PlacementManager.Instance?.ShowPortIndicatorsFor(machine);
        }
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

    private void OnSignalReceived(TutorialSignal signal, object payload)
    {
        TryAdvance(signal, payload);
    }

    public void OnClickShop() => TutorialEventBus.PublishShopOpened();
    public void OnShopBuyButton() => TutorialEventBus.PublishShopBuyClicked();
    public void OnClickShopItem() => TutorialEventBus.PublishShopItemSelected(null);
    public void OnConfirmButton() => TutorialEventBus.PublishPreviewConfirmed();

    public void OnClickInventory() => TutorialEventBus.PublishInventoryOpened();
    public void OnClickRecipeTree() => TutorialEventBus.PublishRecipeTreeOpened();
    public void OnClickInventoryItem() => TutorialEventBus.PublishInventoryItemSelected();
    public void OnClickInventoryItemSell() => TutorialEventBus.PublishInventoryItemSold();
    public void OnClickFixMachineButton() => TutorialEventBus.PublishMachineFixButtonClicked();
    public void OnClickBoostMachineButton() => TutorialEventBus.PublishMachineBoostButtonClicked();
}