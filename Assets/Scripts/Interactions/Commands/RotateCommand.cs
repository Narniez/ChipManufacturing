using UnityEngine;

public class RotateCommand : ICommand
{
    private readonly IGridOccupant _occ;
    private readonly GridService _grid;
    private readonly bool _clockwise;

    private Vector2Int _oldAnchor;
    private GridOrientation _oldOri;
    private Vector2Int _oldSize;

    private Vector2Int _newAnchor;
    private GridOrientation _newOri;
    private Vector2Int _newSize;

    public RotateCommand(IGridOccupant occ, GridService grid, bool clockwise)
    {
        _occ = occ;
        _grid = grid;
        _clockwise = clockwise;
    }

    public bool Execute()
    {
        if (_grid == null || !_grid.HasGrid) return false;

        var comp = _occ as Component; 
        if (comp == null) return false;

        _oldOri = _occ.Orientation;
        _oldAnchor = _occ.Anchor; // removed dynamic
        _oldSize = _occ.BaseSize.OrientedSize(_oldOri);

        _newOri = _clockwise ? _oldOri.RotatedCW() : _oldOri.RotatedCCW();
        _newSize = _occ.BaseSize.OrientedSize(_newOri);
        _newAnchor = _grid.ClampAnchor(_oldAnchor, _newSize);

        var go = comp.gameObject;
        if (!_grid.IsAreaInside(_newAnchor, _newSize) || !_grid.IsAreaFree(_newAnchor, _newSize, go))
            return false;

        // Move occupancy
        _grid.SetAreaOccupant(_oldAnchor, _oldSize, null);
        _grid.SetAreaOccupant(_newAnchor, _newSize, go);

        // Apply placement + world position
        _occ.SetPlacement(_newAnchor, _newOri);
        float yOff = ComputePivotBottomOffset(_occ.DragTransform);
        Vector3 world = AnchorToWorldCenter(_newAnchor, _newSize, yOff);
        _occ.DragTransform.position = world;

        return true;
    }

    public void Undo()
    {
        if (_grid == null) return;

        var comp = _occ as Component;
        if (comp == null) return;

        var go = comp.gameObject;

        _grid.SetAreaOccupant(_newAnchor, _newSize, null);
        _grid.SetAreaOccupant(_oldAnchor, _oldSize, go);

        _occ.SetPlacement(_oldAnchor, _oldOri);
        float yOff = ComputePivotBottomOffset(_occ.DragTransform);
        Vector3 world = AnchorToWorldCenter(_oldAnchor, _oldSize, yOff);
        _occ.DragTransform.position = world;
    }

    private Vector3 AnchorToWorldCenter(Vector2Int anchor, Vector2Int size, float heightOffset)
    {
        float y = _grid.Origin.y + heightOffset;
        float wx = _grid.Origin.x + (anchor.x + size.x * 0.5f) * _grid.CellSize;
        float wz = _grid.Origin.z + (anchor.y + size.y * 0.5f) * _grid.CellSize;
        return new Vector3(wx, y, wz);
    }

    private float ComputePivotBottomOffset(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return 0f;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        float pivotY = root.position.y;
        float bottomY = b.min.y;
        return pivotY - bottomY;
    }
}
