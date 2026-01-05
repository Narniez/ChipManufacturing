using System.Collections.Generic;
using UnityEngine;
using ProceduralMusic;

[CreateAssetMenu(fileName = "DataRegistry", menuName = "Data/DataRegistry")]
public class DataRegistry : ScriptableObject
{
    [System.Serializable]
    public struct MachineEntry { public string id; public MachineData data; public MachineSoundData soundData; }

    [System.Serializable]
    public struct MaterialEntry { public string id; public MaterialData data; }

    [System.Serializable]
    public struct RecipeEntry { public string id; public MachineRecipe data; public RecipeSoundData soundData; }

    public List<MachineEntry> machines = new();
    public List<MaterialEntry> materials = new();
    public List<RecipeEntry> recipes = new();

    private Dictionary<string, MachineData> _machineMap;
    private Dictionary<string, MaterialData> _materialMap;
    private Dictionary<string, MachineRecipe> _recipeMap;

    private Dictionary<MachineData, MachineSoundData> _machineSoundByData;
    private Dictionary<MachineRecipe, RecipeSoundData> _recipeSoundByData;
    private Dictionary<MaterialData, RecipeSoundData> _materialToRecipeSound; // optional mapping if needed

    public static DataRegistry Instance { get; private set; }

    public void OnEnable()
    {
        if (Instance == null) Instance = this;
    }

    public void BuildLookup()
    {
        _machineMap = new Dictionary<string, MachineData>();
        foreach (var e in machines) if (!string.IsNullOrEmpty(e.id) && e.data != null) _machineMap[e.id] = e.data;

        _materialMap = new Dictionary<string, MaterialData>();
        foreach (var e in materials) if (!string.IsNullOrEmpty(e.id) && e.data != null) _materialMap[e.id] = e.data;

        _recipeMap = new Dictionary<string, MachineRecipe>();
        foreach (var r in recipes) if (!string.IsNullOrEmpty(r.id) && r.data != null) _recipeMap[r.id] = r.data;

        _machineSoundByData = new Dictionary<MachineData, MachineSoundData>();
        foreach (var e in machines)
        {
            if (e.data == null || e.soundData == null) continue;
            if (!_machineSoundByData.ContainsKey(e.data)) _machineSoundByData[e.data] = e.soundData;
        }

        _recipeSoundByData = new Dictionary<MachineRecipe, RecipeSoundData>();
        foreach (var r in recipes)
        {
            if (r.data == null || r.soundData == null) continue;
            if (!_recipeSoundByData.ContainsKey(r.data)) _recipeSoundByData[r.data] = r.soundData;
        }
    }

    public MachineData GetMachine(string id)
    {
        if (_machineMap == null) BuildLookup();
        _machineMap.TryGetValue(id, out var d);
        return d;
    }

    public MaterialData GetMaterial(string id)
    {
        if (_materialMap == null) BuildLookup();
        _materialMap.TryGetValue(id, out var d);
        return d;
    }

    public MachineRecipe GetRecipe(string id)
    {
        if (_recipeMap == null) BuildLookup();
        _recipeMap.TryGetValue(id, out var r);
        return r;
    }

    public MachineSoundData GetMachineSoundDataForMachineData(MachineData machineData)
    {
        if (_machineSoundByData == null) BuildLookup();
        if (machineData == null) return null;
        _machineSoundByData.TryGetValue(machineData, out var msd);
        return msd;
    }

    public RecipeSoundData GetRecipeSoundDataForRecipe(MachineRecipe recipe)
    {
        if (_recipeSoundByData == null) BuildLookup();
        if (recipe == null) return null;
        _recipeSoundByData.TryGetValue(recipe, out var rsd);
        return rsd;
    }

    // Convenience: find recipe sound datas for a producing machine
    public List<RecipeSoundData> GetRecipeSoundDatasForProducingMachine(MachineData machineData)
    {
        if (_recipeSoundByData == null) BuildLookup();
        var list = new List<RecipeSoundData>();
        if (machineData == null) return list;
        foreach (var kv in _recipeSoundByData)
        {
            var rsd = kv.Value;
            if (rsd != null && rsd.producingMachine == machineData) list.Add(rsd);
        }
        return list;
    }

    // Fallbacks and helpers left in earlier implementation (FindOrLoad) remain valid...
    public static DataRegistry FindOrLoad()
    {
        if (Instance != null) return Instance;
        var loaded = Resources.LoadAll<DataRegistry>("");
        if (loaded != null && loaded.Length > 0)
        {
            Instance = loaded[0];
            Instance.BuildLookup();
            return Instance;
        }
        return null;
    }
}