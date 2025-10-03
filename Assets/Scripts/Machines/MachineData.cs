using System.Collections.Generic;
using UnityEngine;

public enum MaterialType { None, Silicon, Copper, Plastic, Chip, Circuit }

[CreateAssetMenu(fileName = "MachineData", menuName = "Scriptable Objects/MachineData")]
public class MachineData : ScriptableObject
{
    [Header("Orientation")]
    public GridOrientation defaultOrientation = GridOrientation.North;

    [Header("Basic Info")]
    public string machineName;
    public GameObject prefab;
    public Sprite icon;
    public int cost;

    [Header("Production")]
    public MaterialType inputMaterial;
    public MaterialType outputMaterial;
    public float processingTime = 2f;

    [Header("Size")]
    public Vector2Int size = new Vector2Int(1, 1);

    [Header("Upgrades")]
    public List<MachineUpgrade> upgrades;
}
