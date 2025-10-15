using System.Collections.Generic;
using UnityEngine;

public class MachinePortIndicatorController
{
    private readonly PlacementManager _pm;
    private readonly GridService _grid;
    private readonly List<GameObject> _indicators = new List<GameObject>();

    // Shared transient materials (debug)
    private static Material _matOut;
    private static Material _matIn;

    public MachinePortIndicatorController(PlacementManager pm)
    {
        _pm = pm;
        _grid = pm.GridService;

        // Use Unlit/Color if available, otherwise configure Standard as transparent
        if (_matOut == null)
        {
            _matOut = CreateDebugMat(new Color(1f, 0.93f, 0.2f, 0.85f)); // yellow-ish
        }
        if (_matIn == null)
        {
            _matIn = CreateDebugMat(new Color(0.2f, 0.6f, 1f, 0.85f)); // blue-ish
        }
    }

    public void Cleanup()
    {
        for (int i = 0; i < _indicators.Count; i++)
            if (_indicators[i] != null) Object.Destroy(_indicators[i]);
        _indicators.Clear();
    }

    public void ShowFor(Machine machine)
    {
        Cleanup();
        if (machine == null || _grid == null || !_grid.HasGrid) return;

        var size = machine.BaseSize.OrientedSize(machine.Orientation);

        // Read prefabs from MachineData (optional)
        GameObject inPrefab = machine.Data != null ? machine.Data.inputPortIndicatorPrefab : null;
        GameObject outPrefab = machine.Data != null ? machine.Data.outputPortIndicatorPrefab : null;

        // Outputs (point away)
        bool hadExplicitPorts = machine.Data != null && machine.Data.ports != null && machine.Data.ports.Count > 0;
        if (hadExplicitPorts)
        {
            foreach (var p in machine.Data.ports)
            {
                if (p.kind != MachinePortType.Output) continue;
                GridOrientation worldSide = RotateSide(p.side, machine.Orientation);
                Vector2Int cell = ComputePortCell(machine.Anchor, size, worldSide, p.offset);
                SpawnIndicator(cell, worldSide, outPrefab, _matOut);
            }
        }
        else
        {
            // Fallback: single output on front-center
            GridOrientation worldSide = machine.Orientation;
            Vector2Int cell = ComputePortCell(machine.Anchor, size, worldSide, -1);
            SpawnIndicator(cell, worldSide, outPrefab, _matOut);
        }

        // Inputs (point toward)
        if (hadExplicitPorts)
        {
            foreach (var p in machine.Data.ports)
            {
                if (p.kind != MachinePortType.Input) continue;
                GridOrientation worldSide = RotateSide(p.side, machine.Orientation);
                Vector2Int cell = ComputePortCell(machine.Anchor, size, worldSide, p.offset);
                // Face toward machine = opposite of worldSide
                SpawnIndicator(cell, Opposite(worldSide), inPrefab, _matIn);
            }
        }
    }

    // Prefab-first spawn; if prefab is null, fallback to colored cube
    private void SpawnIndicator(Vector2Int cell, GridOrientation faceDir, GameObject prefab, Material fallbackMat)
    {
        if (!_grid.IsInside(cell)) return;

        Vector3 pos = _grid.CellToWorldCenter(cell, _grid.Origin.y + 0.05f);

        GameObject go;
        if (prefab != null)
        {
            go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, faceDir.ToYaw(), 0f));
            go.name = $"PortIndicator({faceDir})";
            // Disable colliders to avoid blocking interactions
            foreach (var col in go.GetComponentsInChildren<Collider>())
                col.enabled = false;
        }
        else
        {
            // Fallback debug cube
            float s = Mathf.Max(0.5f, _grid.CellSize * 0.6f);
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PortIndicator";
            go.transform.position = pos;
            // Lie flat on ground and then apply yaw (cube fallback)
            go.transform.rotation = Quaternion.Euler(90f, faceDir.ToYaw(), 0f);
            go.transform.localScale = new Vector3(s, s, 1f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && fallbackMat != null) mr.sharedMaterial = fallbackMat;

            var col = go.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        _indicators.Add(go);
    }

    private static Material CreateDebugMat(Color c)
    {
        Shader unlit = Shader.Find("Unlit/Color");
        if (unlit != null)
        {
            var m = new Material(unlit) { color = c };
            return m;
        }

        // Fallback: Standard Transparent
        var std = new Material(Shader.Find("Standard")) { color = c };
        std.SetFloat("_Mode", 3); // Transparent
        std.EnableKeyword("_ALPHABLEND_ON");
        std.renderQueue = 3000;
        var col = std.color; col.a = c.a; std.color = col;
        return std;
    }

    private static GridOrientation RotateSide(GridOrientation local, GridOrientation by)
        => (GridOrientation)(((int)local + (int)by) & 3);

    private static GridOrientation Opposite(GridOrientation o)
        => (GridOrientation)(((int)o + 2) & 3);

    private static Vector2Int ComputePortCell(Vector2Int anchor, Vector2Int size, GridOrientation worldSide, int offset)
    {
        int sideLen = (worldSide == GridOrientation.North || worldSide == GridOrientation.South) ? size.x : size.y;
        int idx = offset < 0 ? Mathf.Max(0, (sideLen - 1) / 2) : Mathf.Clamp(offset, 0, Mathf.Max(0, sideLen - 1));

        switch (worldSide)
        {
            case GridOrientation.North: return new Vector2Int(anchor.x + idx, anchor.y + size.y);
            case GridOrientation.South: return new Vector2Int(anchor.x + idx, anchor.y - 1);
            case GridOrientation.East:  return new Vector2Int(anchor.x + size.x, anchor.y + idx);
            case GridOrientation.West:  return new Vector2Int(anchor.x - 1, anchor.y + idx);
            default: return anchor;
        }
    }
}
