using System.Collections.Generic;
using UnityEngine;

public class PreviewMaterialCache : MonoBehaviour
{
    private struct Entry
    {
        public Renderer Renderer;
        public Material[] Originals;
    }

    private readonly List<Entry> _entries = new List<Entry>();

    public void ApplyPreview(Material previewMat)
    {
        _entries.Clear();
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            var originals = r.sharedMaterials;
            _entries.Add(new Entry { Renderer = r, Originals = originals });

            if (previewMat != null)
            {
                var arr = new Material[originals.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = previewMat;
                r.sharedMaterials = arr;
            }
        }
    }

    public void Restore()
    {
        foreach (var e in _entries)
        {
            if (e.Renderer != null)
                e.Renderer.sharedMaterials = e.Originals;
        }
        _entries.Clear();
    }
}
