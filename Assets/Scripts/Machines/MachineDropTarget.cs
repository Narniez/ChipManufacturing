using System.Collections;
using UnityEngine;

public class MachineDropTarget : MonoBehaviour
{
   public MachineData machineData;

    public bool AcceptMaterial(MaterialData material)
    {
        if (machineData == null || material == null) return false;
        return machineData.inputMaterial == material.materialType;
    }

    public void StartProcessing(MaterialData material)
    {
       StartCoroutine(MaterialProcessingCoroutine(material));
    }

    private IEnumerator MaterialProcessingCoroutine(MaterialData material)
    {
        Debug.Log($"Started processing {material.materialName} in {machineData.machineName}.");
        yield return new WaitForSeconds(machineData.processingTime);
        Debug.Log($"Finished processing {material.materialName} into {machineData.outputMaterial}.");
        // add output material to conveyor belt or inventory here
        yield return null;
    }
}
