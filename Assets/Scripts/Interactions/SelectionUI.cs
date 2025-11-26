using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class SelectionUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI selectionName;
    [SerializeField] private Button rotateLeftButton;
    [SerializeField] private Button rotateRightButton;
    [SerializeField] private Button testDestroyButton;

    [Header("Production")]
    [SerializeField] private Slider productionSlider;
    [SerializeField] private GameObject productionContainer;

    private Action _onRotateLeft;
    private Action _onRotateRight;

    private Machine _observedMachine;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);

        if (rotateLeftButton != null)
            rotateLeftButton.onClick.AddListener(() => _onRotateLeft?.Invoke());

        if (rotateRightButton != null)
            rotateRightButton.onClick.AddListener(() => _onRotateRight?.Invoke());
    }

    public void Show(string title, Action onRotateLeft, Action onRotateRight, bool isBelt)
    {
        _onRotateLeft = onRotateLeft;
        _onRotateRight = onRotateRight;
        if (testDestroyButton != null)
        {
            // avoid adding duplicate listeners on repeated Show calls
            testDestroyButton.onClick.RemoveAllListeners();
            testDestroyButton.gameObject.SetActive(true);
            testDestroyButton.onClick.AddListener(() =>
            {
                var placementManager = PlacementManager.Instance;
                if (placementManager != null)
                {
                    var selectedMachine = placementManager.CurrentSelection as Machine;
                    if (selectedMachine != null)
                    {
                        // Call ReturnMachine to refund the cost
                        EconomyManager.Instance.ReturnMachine(selectedMachine.Data, ref EconomyManager.Instance.playerBalance);
                    }
                    var selectedConveyor = placementManager.CurrentSelection as ConveyorBelt;
                    if(selectedConveyor != null)
                    {
                        // Return conveyor belt cost
                        EconomyManager.Instance.ReturnConveyor(selectedConveyor, ref EconomyManager.Instance.playerBalance);
                    }

                    placementManager.DestroyCurrentSelection();
                }
            });
        }
        if (selectionName != null) selectionName.text = string.IsNullOrEmpty(title) ? "Selected" : title;
        if (panel != null) panel.SetActive(true);

        // Setup production slider subscription for currently selected machine
        SetupProductionForCurrentSelection();
    }

    public void Hide()
    {
        _onRotateLeft = null;
        _onRotateRight = null;
        UnhookObservedMachine();

        if (panel != null) panel.SetActive(false);
    }

    private void SetupProductionForCurrentSelection()
    {
        UnhookObservedMachine();

        var current = PlacementManager.Instance?.CurrentSelection as Machine;
        if (current == null || productionSlider == null)
        {
            if (productionContainer != null) productionContainer.SetActive(false);
            return;
        }

        _observedMachine = current;
        // ensure container visibility
        if (productionContainer != null) productionContainer.SetActive(true);
        productionSlider.minValue = 0f;
        productionSlider.maxValue = 1f;
        productionSlider.value = _observedMachine.IsProducing ? _observedMachine.ProductionProgress : 0f;

        // subscribe to progress updates
        _observedMachine.ProductionProgressChanged += OnProductionProgressChanged;
    }

    private void UnhookObservedMachine()
    {
        if (_observedMachine != null)
        {
            _observedMachine.ProductionProgressChanged -= OnProductionProgressChanged;
            _observedMachine = null;
        }

        if (productionContainer != null) productionContainer.SetActive(false);
    }

    private void OnProductionProgressChanged(float progress)
    {
        if (productionSlider == null) return;
        // Unity event may come from background thread-like timing; ensure set on main thread - but in Unity it's fine.
        productionSlider.value = Mathf.Clamp01(progress);
    }
}
