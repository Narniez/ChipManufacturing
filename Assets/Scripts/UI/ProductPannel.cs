using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Populate a shop UI element from a MachineData ScriptableObject.
/// Attach this component to the shop item (the big button). Wire the child UI fields in the inspector.
/// </summary>
public class ProductPannel : MonoBehaviour
{
    [Header("Machine Data")]
    public MachineData machineData;

    public Button mainButton; // The visible button in the shop that opens details

    [Header("Details panel")]
    public GameObject detailsPanel; // Panel that opens when mainButton is clicked
    public TextMeshProUGUI titleText;
    public Image iconImage;
    public TextMeshProUGUI descriptionText; // optional short description (uses machineName if null)
    public TextMeshProUGUI costText;

    [Header("I/O lists")]
    public Transform inputsContainer;  // parent transform where input items are instantiated
    public Transform outputsContainer; // parent transform where output items are instantiated
    public GameObject materialItemPrefab; // prefab used for each input/output entry

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
            detailsPanel.SetActive(!startDetailsClosed);
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
            detailsPanel.SetActive(next);

            // If opening, ensure populated from current data
            if (next && machineData != null)
                PopulateFrom(machineData);
        }
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
