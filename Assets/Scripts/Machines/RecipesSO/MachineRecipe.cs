using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MachineRecipe", menuName = "Scriptable Objects/Machine/MachineRecipe")]
public class MachineRecipe : ScriptableObject
{

    [Tooltip("Optional display name to identify the recipe")]
    public string name;

    [Tooltip("Materials required to start this recipe (all must be available)")]
    public List<MaterialStack> inputs = new List<MaterialStack>();

    [Tooltip("Materials produced when the recipe finishes")]
    public List<MaterialStack> outputs = new List<MaterialStack>();

    [Tooltip("Override processing time for this recipe (-1 = use MachineData.processingTime)")]
    public float processingTimeOverride = -1f;

}
