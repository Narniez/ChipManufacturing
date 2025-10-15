using System.Collections.Generic;
using UnityEngine;

public enum MaterialType { None, Silicon, Copper, Plastic, Chip, Circuit }

public enum MachinePortType { None, Input, Output }

[System.Serializable]
public struct MachinePortDef
{
    [Tooltip("Input or Output")]
    public MachinePortType kind;

    [Tooltip("Side of the machine in LOCAL space (relative to model 'North'). Will be rotated by current Orientation.")]
    public GridOrientation side;

    [Tooltip("Index along that side (0..sideLength-1). -1 = center of that side.")]
    public int offset;
}

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

    [Header("Conveyor Ports")]
    [Tooltip("Define one or more input/output ports. If empty, a single Output on the front (Orientation) will be assumed.")]
    public List<MachinePortDef> ports = new List<MachinePortDef>();
}
