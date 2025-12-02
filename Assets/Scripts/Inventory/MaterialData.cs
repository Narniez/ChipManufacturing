using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MaterialData", menuName = "Scriptable Objects/MaterialData")]
public class MaterialData : ScriptableObject
{
    [Header("Basic Info")]
    public int id; 
    public string materialName;
    public Sprite icon;


    public GameObject prefab;
    //public something something recipe

    public int cost = 0;
    public string unit = "pcs";
    public int unitScale = 1; // 1 = piece
    public int maxStack = 9999; // soft limit for UI

    //tags for recipes (e.g metal, conductor)
    public List<string> tags = new();

    [Header("Upgrades")]
    [Tooltip("Materials that this material can be upgraded to")]
    public List<MaterialData> upgradeMaterials = new();

    [Header("Cycle machines")]
    [Tooltip("Machine that the material needs to go trhough to get an upgrade")]
    public List<MachineData> requiredMachines = new();



    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            Debug.LogWarning("Material name not valid.");
        }
    }
}
