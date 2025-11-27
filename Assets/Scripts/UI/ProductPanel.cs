using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// Populate a shop pop up UI element from a MachineData ScriptableObject.
/// Attach this component to the shop button of the machine. Wire the child UI fields in the inspector.
/// </summary>
public class ProductPanel : MonoBehaviour
{
    [Header("Machine Data")]
    public MachineData machineData;

    [Header("Main button (this element)")]
    public Button mainButton; 

    [Header("Details panel")]
    public GameObject detailsPanel; // Panel that opens when mainButton is clicked (should be a child of a Canvas)
    public RectTransform detailsPanelRect; // assign in inspector or will be discovered at Awake
    public TextMeshProUGUI titleText;
    public Image iconImage;
    public TextMeshProUGUI descriptionText; // optional short description (uses machineName if null)
    public TextMeshProUGUI costText;

    [Header("I/O lists")]
    public Transform inputsContainer;  // parent transform where input items are instantiated (Horizontal LayoutGroup)
    public Transform outputsContainer; // parent transform where output items are instantiated
    public GameObject materialItemPrefab; // prefab used for each input/output entry (Image + TextMeshProUGUI)

    [Header("Buy")]
    [Tooltip("Button inside the details panel that starts placement / purchase.")]
    public Button buyButton;

    [Header("Positioning")]
    [Tooltip("Optional explicit canvas rect. If not set, will find the nearest parent Canvas.")]
    public RectTransform canvasRect;
    [Tooltip("Pixel offset applied after converting machine world position to canvas anchored position.")]
    public Vector2 screenOffset = new Vector2(0f, 80f);
    [Tooltip("Pixel offset applied when positioning the details panel on top of the UI button that opened it.")]
    public Vector2 uiOffset = new Vector2(0f, 8f);
    [Tooltip("If true, clamp panel to canvas rect so it doesn't go off-screen.")]
    public bool clampToCanvas = true;

    [Header("Options")]
    public bool startDetailsClosed = true;
    public bool showProcessingTime = true;

    void Reset()
    {
        // Try to auto-assign mainButton if component is on same GameObject
        mainButton = GetComponent<Button>();
    }

    void Awake()
    {
        if (mainButton != null)
        {
            mainButton.onClick.RemoveListener(OnMainButtonClicked);
            mainButton.onClick.AddListener(OnMainButtonClicked);
        }

        if (detailsPanel != null)
        {
            if (detailsPanelRect == null)
                detailsPanelRect = detailsPanel.GetComponent<RectTransform>();
            detailsPanel.SetActive(!startDetailsClosed);
        }

        if (canvasRect == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();
        }
    }

    void Start()
    {
        // If a MachineData is already assigned in inspector, populate at start
        if (machineData != null)
            PopulateFrom(machineData);
    }

    /// <summary>
    /// Public API: assign a MachineData and update UI.
    /// Call this from other scripts (e.g. shop generator).
    /// </summary>
    public void SetMachine(MachineData data)
    {
        machineData = data;
        PopulateFrom(machineData);
    }

    void OnMainButtonClicked()
    {
        if (detailsPanel != null)
        {
            bool next = !detailsPanel.activeSelf;

            // If opening, ensure populated from current data and position over the button.
            if (next && machineData != null)
            {
                PopulateFrom(machineData);

                // Prefer UI-button positioning when opening from the button.
                RectTransform buttonRT = mainButton != null ? mainButton.GetComponent<RectTransform>() : GetComponent<RectTransform>();
                if (buttonRT != null)
                    PositionPanelOverButton(buttonRT);

                // Assign buy button listener for THIS product only (captures current machineData).
                SetupBuyButtonFor(machineData);
            }
            else if (!next)
            {
                // closing: cleanup listeners to avoid stale handlers
                CleanupBuyButton();
            }

            detailsPanel.SetActive(next);
        }
    }

    /// <summary>
    /// Called when the buy button is pressed in the details panel.
    /// Starts placement for the specified MachineData and then closes the shop.
    /// This method is NOT used as a permanent listener; we attach lambdas that capture target data.
    /// </summary>
    void OnBuyClicked_Internal(MachineData target)
    {
        if (target == null)
        {
            Debug.LogWarning("ProductPanel.OnBuyClicked_Internal: target MachineData is null.");
            return;
        }

        if (PlacementManager.Instance == null)
        {
            Debug.LogWarning("ProductPanel.OnBuyClicked_Internal: PlacementManager.Instance is null.");
        }
        else
        {
            PlacementManager.Instance.StartPlacement(target);
        }

        // Hide details panel after starting placement
        Hide();
    }

    /// <summary>
    /// Reuse this details panel for a machine and position it over the given world position.
    /// - worldCamera: if your Canvas is Screen Space - Camera or WorldSpace, pass the camera used by the canvas (usually Camera.main).
    ///   If your Canvas is Screen Space - Overlay pass null or leave worldCamera null.
    /// </summary>
    public void ShowForMachine(MachineData data, Vector3 worldPosition, Camera worldCamera = null)
    {
        if (detailsPanel == null)
            return;

        SetMachine(data);
        PositionPanelOverWorldPoint(worldPosition, worldCamera);

        // If details panel is shared, set buy button to this machine (captures data)
        SetupBuyButtonFor(data);

        detailsPanel.SetActive(true);
    }

    /// <summary>
    /// Hide the details panel.
    /// </summary>
    public void Hide()
    {
        if (detailsPanel != null)
            detailsPanel.SetActive(false);

        // cleanup listeners so shared button doesn't keep stale handlers
        CleanupBuyButton();
    }

    /// <summary>
    /// Assign the buy button so it triggers placement for the provided MachineData only.
    /// Removes previous listeners to avoid multiple invocations from earlier ProductPanel instances.
    /// </summary>
    private void SetupBuyButtonFor(MachineData data)
    {
        if (buyButton == null) return;

        // Remove all previous listeners on the shared buy button (intent: this UI is single-purpose)
        buyButton.onClick.RemoveAllListeners();

        // Capture the data in a local variable to avoid closure capturing mutable field
        MachineData captured = data;

        // Add a listener that calls the internal handler with the captured MachineData
        buyButton.onClick.AddListener(() => OnBuyClicked_Internal(captured));
    }

    // Remove listeners to avoid stale references when panel is closed or another item opens it.
    private void CleanupBuyButton()
    {
        if (buyButton == null) return;
        buyButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Position the details panel so it is aligned with the given UI button RectTransform.
    /// This converts the button's world/screen position into the canvas local coordinates and applies uiOffset.
    /// </summary>
    public void PositionPanelOverButton(RectTransform buttonRect)
    {
        if (buttonRect == null) return;

        if (detailsPanelRect == null)
        {
            if (detailsPanel != null)
                detailsPanelRect = detailsPanel.GetComponent<RectTransform>();
            if (detailsPanelRect == null) return;
        }

        if (canvasRect == null)
        {
            var canvas = detailsPanel.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();
        }
        if (canvasRect == null) return;

        Canvas parentCanvas = canvasRect.GetComponent<Canvas>();
        Camera canvasCam = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? parentCanvas.worldCamera : null;

        // Get screen point of the button's pivot
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCam, buttonRect.position);

        // Convert to local point in canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCam, out Vector2 localPoint);

        // Apply UI offset (x,y in pixels)
        Vector2 anchored = localPoint + uiOffset;

        if (clampToCanvas)
            anchored = ClampToCanvas(anchored, detailsPanelRect, canvasRect);

        detailsPanelRect.anchoredPosition = anchored;
    }

    /// <summary>
    /// Convert a world position to canvas local position and set the panel anchored position.
    /// Honors the optional screenOffset and will clamp to canvasRect if enabled.
    /// </summary>
    public void PositionPanelOverWorldPoint(Vector3 worldPos, Camera worldCamera = null)
    {
        if (detailsPanelRect == null)
        {
            if (detailsPanel != null)
                detailsPanelRect = detailsPanel.GetComponent<RectTransform>();
            if (detailsPanelRect == null)
                return;
        }

        if (canvasRect == null)
        {
            var canvas = detailsPanel.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();
        }

        if (canvasRect == null)
            return;

        // Choose camera: if canvas render mode is Overlay we don't need a camera for ScreenPoint conversions.
        Canvas parentCanvas = canvasRect.GetComponent<Canvas>();
        Camera camToUse = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            camToUse = worldCamera ?? Camera.main;

        Vector3 screenPoint = (camToUse != null) ? camToUse.WorldToScreenPoint(worldPos) : Camera.main.WorldToScreenPoint(worldPos);
        // Convert screen point to local point in canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, camToUse, out Vector2 localPoint);

        Vector2 anchored = localPoint + screenOffset;
        if (clampToCanvas)
            anchored = ClampToCanvas(anchored, detailsPanelRect, canvasRect);

        detailsPanelRect.anchoredPosition = anchored;
    }

    Vector2 ClampToCanvas(Vector2 anchoredPosition, RectTransform panel, RectTransform canvas)
    {
        Vector2 min = canvas.rect.min - panel.rect.min;
        Vector2 max = canvas.rect.max - panel.rect.max;
        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, min.x, max.x);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, min.y, max.y);
        return anchoredPosition;
    }

    void PopulateFrom(MachineData data)
    {
        if (data == null)
            return;

        // Title and icon
        if (titleText != null)
            titleText.text = string.IsNullOrEmpty(data.machineName) ? "Unnamed Machine" : data.machineName;

        if (iconImage != null)
            iconImage.sprite = data.icon;

        // Description - MachineData doesn't have a description field so we synthesize one.
        if (descriptionText != null)
        {
            string desc = $"Size: {data.size.x}x{data.size.y}";
            if (showProcessingTime && data.processingTime > 0f)
                desc += $"\nDefault processing: {data.processingTime:0.##}s";
            if (data.HasRecipes)
                desc += $"\nRecipes: {data.recipes.Count}";
            descriptionText.text = desc;
        }

        // Cost
        if (costText != null)
            costText.text = $"Cost: {data.cost}";

        // Populate inputs/outputs
        ClearContainer(inputsContainer);
        ClearContainer(outputsContainer);

        // If recipes exist, show the first recipe's I/O by default.
        if (data.HasRecipes && data.recipes.Count > 0)
        {
            var recipe = data.recipes[0];
            if (recipe.inputs != null)
                foreach (var ms in recipe.inputs)
                    CreateMaterialItem(ms, inputsContainer);

            if (recipe.outputs != null)
                foreach (var ms in recipe.outputs)
                    CreateMaterialItem(ms, outputsContainer);
        }
        else
        {
            // Legacy single input/output mode
            if (data.inputMaterial != null)
            {
                var ms = new MaterialStack { material = data.inputMaterial, amount = 1 };
                CreateMaterialItem(ms, inputsContainer);
            }

            if (data.outputMaterial != null)
            {
                var ms = new MaterialStack { material = data.outputMaterial, amount = 1 };
                CreateMaterialItem(ms, outputsContainer);
            }
        }
    }

    void CreateMaterialItem(MaterialStack ms, Transform parent)
    {
        if (parent == null || materialItemPrefab == null || ms.material == null)
            return;

        var go = Instantiate(materialItemPrefab, parent);

        // Try to find an Image component on root or first child
        var img = go.GetComponent<Image>();
        if (img == null)
            img = go.GetComponentInChildren<Image>();
        if (img != null)
            img.sprite = ms.material.icon;

        // Try to find a TMP label
        var label = go.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            string unit = string.IsNullOrEmpty(ms.material.unit) ? "" : $" {ms.material.unit}";
            label.text = $"{ms.material.materialName} x{ms.amount}{unit}";
        }
    }

    void ClearContainer(Transform parent)
    {
        if (parent == null)
            return;
        // Destroy all children (runtime-only)
        for (int i = parent.childCount - 1; i >= 0; --i)
        {
            var c = parent.GetChild(i).gameObject;
            Destroy(c);
        }
    }
}
