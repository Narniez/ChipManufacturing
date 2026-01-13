using System.Collections;
using UnityEngine;

public class ScenarioB_AutoPlaceBelts : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlacementManager placementManager;

    [Header("Layout")]
    [SerializeField] private Vector2Int startAnchor = new Vector2Int(5, 5);
    [SerializeField] private int columns = 5;
    [SerializeField] private int rowsPerColumn = 20;
    [SerializeField, Tooltip("Empty grid cells between columns (not counting the belt cell itself).")]
    private int columnSpacingCells = 2;

    [Header("Prefabs")]
    [SerializeField] private bool useEconomy = false;

    private GridService _grid;

    private IEnumerator Start()
    {
        if (placementManager == null) placementManager = PlacementManager.Instance;
        if (placementManager == null)
        {
            Debug.LogError("[ScenarioB_AutoPlaceBelts] No PlacementManager.");
            yield break;
        }

        // waiting for grid to be ready
        while (placementManager.GridService == null || !placementManager.GridService.HasGrid)
            yield return null;

        _grid = placementManager.GridService;

        yield return new WaitForSecondsRealtime(0.5f);

        int placed = BuildSerpentine();
        Debug.Log($"[ScenarioB_AutoPlaceBelts] Placed belts={placed} (target={columns * rowsPerColumn})");
    }

    private int BuildSerpentine()
    {
        var straightPrefab = placementManager.GetConveyorPrefab(false);
        if (straightPrefab == null)
        {
            Debug.LogError("[ScenarioB_AutoPlaceBelts] Straight belt prefab missing.");
            return 0;
        }

        int placed = 0;

        int xStep = 1 + Mathf.Max(0, columnSpacingCells);

        // col0 goes up, col1 goes down, etc.
        ConveyorBelt prev = null;

        for (int c = 0; c < columns; c++)
        {
            int x = startAnchor.x + c * xStep;
            bool goingUp = (c % 2 == 0);

            // determining start/end Y for this column
            int yStart = goingUp ? startAnchor.y : (startAnchor.y + rowsPerColumn - 1);
            int yEndExclusive = goingUp ? (startAnchor.y + rowsPerColumn) : (startAnchor.y - 1);
            int yStep = goingUp ? 1 : -1;

            if (c > 0)
            {

                int prevX = startAnchor.x + (c - 1) * xStep;
                int bridgeY = prev != null ? prev.Anchor.y : yStart;

                // placing belts from prevX+1 to x (inclusive) along bridgeY
                for (int bx = prevX + 1; bx <= x; bx++)
                {
                    var cell = new Vector2Int(bx, bridgeY);
                    var ori = GridOrientation.East;

                    var b = PlaceStraight(straightPrefab, cell, ori);
                    if (b == null) continue;

                    Link(prev, b);
                    PromoteCornerIfNeeded(prev, b);
                    prev = b;
                    placed++;
                }

                while (prev != null && prev.Anchor.y != yStart)
                {
                    int dir = prev.Anchor.y < yStart ? 1 : -1;
                    var nextCell = new Vector2Int(x, prev.Anchor.y + dir);
                    var nextOri = dir > 0 ? GridOrientation.North : GridOrientation.South;

                    var b = PlaceStraight(straightPrefab, nextCell, nextOri);
                    if (b == null) break;

                    Link(prev, b);
                    PromoteCornerIfNeeded(prev, b);
                    prev = b;
                    placed++;
                }
            }

            // placing the column belts
            for (int y = yStart; y != yEndExclusive; y += yStep)
            {
                var cell = new Vector2Int(x, y);
                var ori = goingUp ? GridOrientation.North : GridOrientation.South;

                var b = PlaceStraight(straightPrefab, cell, ori);
                if (b == null) continue;

                Link(prev, b);
                PromoteCornerIfNeeded(prev, b);
                prev = b;
                placed++;
            }
        }

        return placed;
    }

    private ConveyorBelt PlaceStraight(GameObject straightPrefab, Vector2Int cell, GridOrientation orientation)
    {
        if (_grid == null || !_grid.HasGrid) return null;
        if (!_grid.IsInside(cell)) return null;
        if (!_grid.IsAreaFree(cell, Vector2Int.one)) return null;

        if (useEconomy)
        {
            var econ = EconomyManager.Instance;
            if (econ != null)
            {
                var template = straightPrefab.GetComponent<ConveyorBelt>();
                int cost = template != null ? template.Cost : 0;
                if (econ.playerBalance < cost) return null;
                econ.PurchaseConveyor(template, ref econ.playerBalance);
            }
        }

        Vector3 pos = placementManager.AnchorToWorldCenter(cell, Vector2Int.one, 0f);
        var go = Instantiate(straightPrefab, pos, orientation.ToRotation());
        _grid.SetAreaOccupant(cell, Vector2Int.one, go);

        var belt = go.GetComponent<ConveyorBelt>();
        if (belt == null)
        {
            Destroy(go);
            return null;
        }

        belt.SetPlacement(cell, orientation);
        return belt;
    }

    private static void Link(ConveyorBelt a, ConveyorBelt b)
    {
        if (a == null || b == null) return;
        a.NextInChain = b;
        b.PreviousInChain = a;
    }

    private void PromoteCornerIfNeeded(ConveyorBelt parent, ConveyorBelt child)
    {
        if (parent == null || child == null) return;
        if (parent.IsTurnPrefab) return;

        Vector2Int delta = child.Anchor - parent.Anchor;
        var outgoing = DeltaToOrientation(delta);
        if (!outgoing.HasValue) return;

        if (outgoing.Value == parent.Orientation) return;

        var incoming = parent.Orientation;

        var turnKind =
            outgoing.Value == incoming.RotatedCW() ? ConveyorBelt.BeltTurnKind.Right :
            outgoing.Value == incoming.RotatedCCW() ? ConveyorBelt.BeltTurnKind.Left :
            ConveyorBelt.BeltTurnKind.None;

        if (turnKind == ConveyorBelt.BeltTurnKind.None) return;

        var replaced = placementManager.ReplaceConveyorPrefab(parent, useTurnPrefab: true, overrideOrientation: outgoing.Value, turnKind: turnKind);
        replaced?.LockCorner();

        if (replaced != null)
        {
            // reconnecting previous
            var prev = parent.PreviousInChain;
            if (prev != null)
            {
                prev.NextInChain = replaced;
                replaced.PreviousInChain = prev;
            }

            // reconnecting next
            replaced.NextInChain = child;
            child.PreviousInChain = replaced;
        }
    }

    private static GridOrientation? DeltaToOrientation(Vector2Int d)
    {
        if (d == Vector2Int.up) return GridOrientation.North;
        if (d == Vector2Int.right) return GridOrientation.East;
        if (d == Vector2Int.down) return GridOrientation.South;
        if (d == Vector2Int.left) return GridOrientation.West;
        return null;
    }
}
