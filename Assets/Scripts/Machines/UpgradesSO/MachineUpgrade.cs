using UnityEngine;

[CreateAssetMenu(fileName = "MachineUpgrade", menuName = "Scriptable Objects/Machine/MachineUpgrade")]
public class MachineUpgrade : ScriptableObject
{
    public float prrocessingSpeedMultiplier = 1.2f;
    //public float energyConsumptionMultiplier = 1.1f; ?
    public int productionOutputIncrease = 1;
    public GameObject visualChange;

    //new material as input?
    //new material as outpu?
    //particle effect for upgrade?
    
}
