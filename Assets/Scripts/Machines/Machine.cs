using System.Collections;
using UnityEngine;

public class Machine : MonoBehaviour, IInteractable, IDraggable
{
    private MachineData data;
    private int upgradeLevel = 0;
    private Coroutine productionRoutine;

    public event System.Action<MaterialType, Vector3> OnMaterialProduced;

    // IDraggable
    public bool CanDrag => true;
    public Transform DragTransform => transform;
    public void Initialize(MachineData machineData)
    {
        data = machineData;
        StartProduction();
    }

    private void StartProduction()
    {
        if (productionRoutine != null)
            StopCoroutine(productionRoutine);

        productionRoutine = StartCoroutine(ProductionLoop());
    }

    private IEnumerator ProductionLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(data.processingTime);
            ProduceOutput();
        }
    }

    private void ProduceOutput()
    {
        OnMaterialProduced?.Invoke(data.outputMaterial, transform.position);
    }

    public void Upgrade()
    {
        if (upgradeLevel < data.upgrades.Count)
        {
            var upgrade = data.upgrades[upgradeLevel];
            data.processingTime *= upgrade.prrocessingSpeedMultiplier;
            upgradeLevel++;
            StartProduction(); 
        }
    }

    // IInteractable
    public void OnTap()
    {
        Debug.Log($"Machine {data.machineName} tapped. Upgrade level: {upgradeLevel}");
    }

    public void OnHold()
    {
        // Optional: add visual feedback (highlight, scale, etc.). Dragging is handled by PlacementManager.
        // e.g., GetComponent<Renderer>()?.material.SetFloat("_Outline", 1f);
    }

    public void OnDragStart()
    {
        // Optional: visuals/feedback when picked up
    }

    public void OnDrag(Vector3 worldPosition)
    {
        DragTransform.position = worldPosition;
    }

    public void OnDragEnd()
    {
        // Optional: visuals/feedback when dropped
    }
}
