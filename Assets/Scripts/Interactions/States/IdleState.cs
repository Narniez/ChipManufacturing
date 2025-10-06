using UnityEngine;

public class IdleState : BasePlacementState
{
    public IdleState(PlacementManager ctx) : base(ctx) { }

    public override void OnTap(IInteractable target, Vector2 screen, Vector3 world)
    {
        // Tap a grid occupant -> select and show UI
        if (target is IGridOccupant occ &&
            PlaceMan.GridService != null &&
            PlaceMan.GridService.HasGrid)
        {
            PlaceMan.SetState(new SelectingState(PlaceMan, occ));
        }
    }

    public override void OnHoldStart(IInteractable target, Vector2 screen, Vector3 world)
    {
        // Only start drag when a grid occupant is held
        if (target is IGridOccupant occ && occ.CanDrag &&
            PlaceMan.GridService != null && PlaceMan.GridService.HasGrid)
        {
            PlaceMan.SetState(new DraggingState(PlaceMan, occ, world));
        }
    }
}
