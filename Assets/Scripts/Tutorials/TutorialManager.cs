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
        //Debug.Log($"TutorialManager.OnEnable instanceID={this.GetInstanceID()}, gameObject={gameObject.name}");
        // Single subscription to generic tutorial stream — ensures all PublishSignal(...) calls are observed.
        TutorialEventBus.OnSignalPublished += OnSignalReceived;

        if (autoStart) StartSequence();
    }

    private void OnDisable()
    {
        TutorialEventBus.OnSignalPublished -= OnSignalReceived;
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

    // Single generic handler for all tutorial signals (subscribed in OnEnable).
    private void OnSignalReceived(TutorialSignal signal, object payload)
    {
        //Debug.Log($"TutorialManager.OnSignalReceived: signal={signal}, payload={payload}");
        TryAdvance(signal, payload);
    }

    // Keep these helper methods for UI wiring (they still call the bus helpers)
    public void OnClickShop() => TutorialEventBus.PublishShopOpened();
    public void OnShopBuyButton()
    {
        //Debug.Log($"TutorialManager.OnShopBuyButton called (instance={this.GetInstanceID()})");
        TutorialEventBus.PublishShopBuyClicked();
    }
    public void OnClickShopItem() => TutorialEventBus.PublishShopItemSelected(null);
    public void OnConfirmButton() => TutorialEventBus.PublishPreviewConfirmed();

    public void OnClickInventory() => TutorialEventBus.PublishInventoryOpened();
    public void OnClickRecipeTree() => TutorialEventBus.PublishRecipeTreeOpened();
    public void OnClickInventoryItem() => TutorialEventBus.PublishInventoryItemSelected();
    public void OnClickInventoryItemSell() => TutorialEventBus.PublishInventoryItemSold();
    public void OnClickFixMachineButton() => TutorialEventBus.PublishMachineFixButtonClicked();
    public void OnClickBoostMachineButton() => TutorialEventBus.PublishMachineBoostButtonClicked();

    //public void OnMachineProducedMaterial() => TutorialEventBus.PublishMaterialProduced();
}