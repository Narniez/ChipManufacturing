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
        // Subscribe to the tutorial bus
        TutorialEventBus.OnShopOpened += Bus_ShopOpened;
        TutorialEventBus.OnShopItemSelected += Bus_ShopItemSelected;
        TutorialEventBus.OnShopBuyClicked += Bus_ShopBuyClicked;
        TutorialEventBus.OnPreviewStarted += Bus_PreviewStarted;
        TutorialEventBus.OnPreviewConfirmed += Bus_PreviewConfirmed;
        TutorialEventBus.OnOccupantPlaced += Bus_OccupantPlaced;
        //TutorialEventBus.OnSelectionChanged += Bus_SelectionChanged;

        if (autoStart) StartSequence();
    }

    private void OnDisable()
    {
        // Unsubscribe
        TutorialEventBus.OnShopOpened -= Bus_ShopOpened;
        TutorialEventBus.OnShopItemSelected -= Bus_ShopItemSelected;
        TutorialEventBus.OnShopBuyClicked -= Bus_ShopBuyClicked;
        TutorialEventBus.OnPreviewStarted -= Bus_PreviewStarted;
        TutorialEventBus.OnPreviewConfirmed -= Bus_PreviewConfirmed;
        TutorialEventBus.OnOccupantPlaced -= Bus_OccupantPlaced;
        //TutorialEventBus.OnSelectionChanged -= Bus_SelectionChanged;
    }

    private void Update()
    {
        // Allow narration-only steps (no signal) to advance on click/tap
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
        if (sequence == null || _index >= sequence.steps.Count)
        {
            overlay?.Show(false);
            return;
        }

        var step = sequence.steps[_index];
        overlay?.Show(true);

        // Resolve highlight target by path/name (optional)
        RectTransform target = ResolveRect(step.highlightTargetPath);
        overlay?.ConfigureStep(step, target);
    }

    private bool HasActiveStep => sequence != null && _index >= 0 && _index < sequence.steps.Count;

    private RectTransform ResolveRect(string pathOrName)
    {
        if (string.IsNullOrEmpty(pathOrName)) return null;
        var go = GameObject.Find(pathOrName);
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    // ---- Bus handlers -> try to advance when the expected signal arrives ----

    private void TryAdvance(TutorialSignal sig, object payload)
    {
        if (!HasActiveStep) return;

        var step = sequence.steps[_index];
        if (step.waitForSignal != sig) return;

        // Optional filter for shop-related steps
        if (step.machineFilter != null && payload is MachineData md && md != step.machineFilter)
            return;

        Advance();
    }

    private void Bus_ShopOpened() => TryAdvance(TutorialSignal.ShopOpened, null);
    private void Bus_ShopItemSelected(MachineData data) => TryAdvance(TutorialSignal.ShopItemSelected, data);
    private void Bus_ShopBuyClicked(MachineData data) => TryAdvance(TutorialSignal.ShopBuyClicked, data);
    private void Bus_PreviewStarted(GameObject prefab) => TryAdvance(TutorialSignal.PreviewStarted, prefab);
    private void Bus_PreviewConfirmed() => TryAdvance(TutorialSignal.PreviewConfirmed, null);
    private void Bus_OccupantPlaced(IGridOccupant occ) => TryAdvance(TutorialSignal.MachinePlaced, occ);
    //private void Bus_SelectionChanged(IGridOccupant occ) => TryAdvance(TutorialSignal.SelectionChanged, occ);
}