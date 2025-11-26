using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MaterialRegistry", menuName = "Scriptable Objects/MaterialRegistry")]
public class MaterialRegistry : ScriptableObject
{
    [Tooltip("List all MaterialData assets used by the game (assign in the Editor).")]
    public List<MaterialData> materials = new List<MaterialData>();

    private Dictionary<int, MaterialData> _byId;
    private Dictionary<MaterialType, MaterialData> _byType;

    public MaterialData GetById(int id)
    {
        Ensure();
        return _byId.TryGetValue(id, out var m) ? m : null;
    }

    public MaterialData GetByType(MaterialType t)
    {
        Ensure();
        return _byType.TryGetValue(t, out var m) ? m : null;
    }

    private void Ensure()
    {
        if (_byId != null) return;
        _byId = new Dictionary<int, MaterialData>();
        _byType = new Dictionary<MaterialType, MaterialData>();
        foreach (var m in materials)
        {
            if (m == null) continue;
            if (!_byId.ContainsKey(m.id)) _byId[m.id] = m;
            if (!_byType.ContainsKey(m.materialType)) _byType[m.materialType] = m;
        }
    }
}