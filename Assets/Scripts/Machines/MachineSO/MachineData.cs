using System.Collections.Generic;
using UnityEngine;

public enum MaterialType { None, Silicon, Copper, Plastic, Chip, Circuit, Wafer, TransistorWafer}

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

[System.Serializable]
public struct MaterialStack
{
    //public MaterialType material;
    public MaterialData material;
    [Min(1)] public int amount;
}


[CreateAssetMenu(fileName = "MachineData", menuName = "Scriptable Objects/Machine/MachineData")]
public class MachineData : ScriptableObject
{
    [Header("Orientation")]
    public GridOrientation defaultOrientation = GridOrientation.North;

    [Header("Basic Info")]
    public string machineName;
    public GameObject prefab;
    public Sprite icon;
    public int cost;

    [Header("Production (Legacy)")]
    [Tooltip("Legacy single-input mode. If Recipes list is NOT empty, this is ignored.")]
    public MaterialData inputMaterial; /*= MaterialType.None;*/
    [Tooltip("Legacy single-output mode. If Recipes list is NOT empty, this is ignored.")]
    public MaterialData outputMaterial; /*= MaterialType.None;*/
    [Tooltip("Default processing time. Recipes can override per-recipe.")]
    public float processingTime = 2f;

    [Header("Recipes (Preferred)")]
    [Tooltip("If this list has entries, the machine runs using recipes instead of legacy single input/output.")]
    public List<MachineRecipe> recipes = new List<MachineRecipe>();

    [Header("Size")]
    public Vector2Int size = new Vector2Int(1, 1);

    [Header("Repair Minigame")]
    public RepairMinigame repairMinigame;

    [Header("Upgrades")]
    public List<MachineUpgrade> upgrades;

    [Header("Conveyor Ports")]
    [Tooltip("Define one or more input/output ports. If empty, a single Output on the front (Orientation) will be assumed.")]
    public List<MachinePortDef> ports = new List<MachinePortDef>();

    [Header("Port Indicators (Optional)")]
    [Tooltip("Prefab used to mark INPUT port cells (should visually point toward the machine).")]
    public GameObject inputPortIndicatorPrefab;
    [Tooltip("Prefab used to mark OUTPUT port cells (should visually point away from the machine).")]
    public GameObject outputPortIndicatorPrefab;

    public bool HasRecipes => recipes != null && recipes.Count > 0;

    [Header("Machine breaking")]
    public float chanceIncreasePerOutput = 2f;
    public float minimunChanceToBreak = 20f;
    public GameObject brokenMachineIndicator;
    public ParticleSystem brokenMachineParticleEffect;
}
