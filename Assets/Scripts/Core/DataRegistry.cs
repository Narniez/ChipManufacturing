using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DataRegistry", menuName = "Data/DataRegistry")]
public class DataRegistry : ScriptableObject
{
    [System.Serializable]
    public struct MachineEntry { public string id; public MachineData data; }

    [System.Serializable]
    public struct MaterialEntry { public string id; public MaterialData data; }

    public List<MachineEntry> machines = new();
    public List<MaterialEntry> materials = new();

    private Dictionary<string, MachineData> _machineMap;
    private Dictionary<string, MaterialData> _materialMap;

    public void BuildLookup()
    {
        _machineMap = new Dictionary<string, MachineData>();
        foreach (var e in machines) if (!string.IsNullOrEmpty(e.id) && e.data != null) _machineMap[e.id] = e.data;

        _materialMap = new Dictionary<string, MaterialData>();
        foreach (var e in materials) if (!string.IsNullOrEmpty(e.id) && e.data != null) _materialMap[e.id] = e.data;
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
}