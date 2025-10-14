using System.Collections.Generic;
using UnityEngine;

public class MaterialVisualRegistry : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public MaterialType material;
        public GameObject itemPrefab;
    }

    [Header("Material -> Item Prefab")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    private readonly Dictionary<MaterialType, GameObject> _map = new Dictionary<MaterialType, GameObject>();

    public static MaterialVisualRegistry Instance { get; private set; }

    private void Awake()
    {
        // Singleton in scene
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple MaterialVisualRegistry instances in scene. Using the first one.");
            return;
        }
        Instance = this;
        Rebuild();
    }

    private void OnValidate()
    {
        // Keep dictionary synced while editing
        Rebuild();
    }

    public void Rebuild()
    {
        _map.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            _map[e.material] = e.itemPrefab;
        }
    }

    public GameObject GetPrefab(MaterialType material)
    {
        if (material == MaterialType.None) return null;
        return _map.TryGetValue(material, out var go) ? go : null;
    }
}
