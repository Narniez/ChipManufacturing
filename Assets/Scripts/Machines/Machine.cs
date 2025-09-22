using System.Collections;
using UnityEngine;

public class Machine : MonoBehaviour, IInteractable
{
    private MachineData data;
    private int upgradeLevel = 0;
    private Coroutine productionRoutine;

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
        // Fire event or directly tell ConveyorManager
        OnMaterialProduced?.Invoke(data.outputMaterial, transform.position);
        //Debug.Log($"Machine {data.machineName} produced {data.outputMaterial}");
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

    public void OnTap()
    {
        // Open UI, show upgrade button, stats, etc.
        Debug.Log($"Machine {data.machineName} tapped. Upgrade level: {upgradeLevel}");
    }

    public void OnHold()
    {
        Debug.Log("Machine hold interaction not implemented."); 
    }

    public event System.Action<MaterialType, Vector3> OnMaterialProduced;
}
