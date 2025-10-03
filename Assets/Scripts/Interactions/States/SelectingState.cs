using UnityEngine;

public class SelectingState : BasePlacementState
{
    private readonly IGridOccupant _selected;

    public SelectingState(PlacementManager ctx, IGridOccupant selected) : base(ctx)
    {
        _selected = selected;
    }

    public override void Enter()
    {
        var ui = PlaceMan.SelectionUI;
        if (ui == null || _selected == null) return;

        // Resolve display name
        var comp = (_selected as Component);
        string name = comp.GetComponent<Machine>()?.Data?.machineName ?? comp.name;

        // Wire rotate actions through the command system
        ui.Show(
            name,
            onRotateLeft: () => PlaceMan.ExecuteRotateCommand(_selected, clockwise: false),
            onRotateRight: () => PlaceMan.ExecuteRotateCommand(_selected, clockwise: true)
        );
    }

    public override void Exit()
    {
        PlaceMan.SelectionUI?.Hide();
    }

    public override void OnTap(IInteractable target, Vector2 screen, Vector3 world)
    {
        // Tap empty or non-occupant -> close UI
        if (!(target is IGridOccupant occ))
        {
            PlaceMan.SetState(new IdleState(PlaceMan));
            return;
        }

        // Tap another occupant -> switch selection
        if (!ReferenceEquals(occ, _selected))
        {
            PlaceMan.SetState(new SelectingState(PlaceMan, occ));
        }
        // Tap same occupant -> keep selection (no-op)
    }

    public override void OnHoldStart(IInteractable target, Vector2 screen, Vector3 world)
    {
        // Begin dragging from selection
        if (target is IGridOccupant occ &&
            occ.CanDrag &&
            PlaceMan.GridService != null &&
            PlaceMan.GridService.HasGrid)
        {
            PlaceMan.SetState(new DraggingState(PlaceMan, occ, world));
        }
    }

    public override void OnRotateRequested()
    {
        if (_selected == null) return;
        PlaceMan.ExecuteRotateCommand(_selected, clockwise: true);
    }
}
