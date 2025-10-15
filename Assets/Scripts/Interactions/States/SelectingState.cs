using UnityEngine;

public class SelectingState : BasePlacementState
{
    private readonly IGridOccupant _selected;
    private BeltChainPreviewController _beltPreviews;
    private MachinePortIndicatorController _portIndicators;

    public SelectingState(PlacementManager ctx, IGridOccupant selected) : base(ctx)
    {
        _selected = selected;
    }

    public override void Enter()
    {
        PlaceMan.SetCurrentSelection(_selected);

        var ui = PlaceMan.SelectionUI;
        if (ui == null || _selected == null) return;

        var comp = (_selected as Component);
        string name = comp.GetComponent<Machine>()?.Data?.machineName ?? comp.name;

        ui.Show(
            name,
            onRotateLeft:  () => RotateAndRefresh(clockwise: false),
            onRotateRight: () => RotateAndRefresh(clockwise: true)
        );

        var belt = comp.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            _beltPreviews = new BeltChainPreviewController(PlaceMan);
            _beltPreviews.ShowOptionsFrom(belt);
        }

        var machine = comp.GetComponent<Machine>();
        if (machine != null)
        {
            _portIndicators = new MachinePortIndicatorController(PlaceMan);
            _portIndicators.ShowFor(machine);
        }
    }

    public override void Exit()
    {
        PlaceMan.SetCurrentSelection(null);

        PlaceMan.SelectionUI?.Hide();
        _beltPreviews?.Cleanup();
        _beltPreviews = null;
        _portIndicators?.Cleanup();
        _portIndicators = null;
    }

    public override void OnTap(IInteractable target, Vector2 screen, Vector3 world)
    {
        var prev = (target as Component)?.GetComponent<ConveyorPreview>();
        if (prev != null && _beltPreviews != null)
        {
            var placed = _beltPreviews.PlaceFromPreview(prev);
            _beltPreviews.Cleanup();

            if (placed != null)
            {
                PlaceMan.SetState(new SelectingState(PlaceMan, placed));
                return;
            }
        }

        if (!(target is IGridOccupant occ))
        {
            PlaceMan.SetState(new IdleState(PlaceMan));
            return;
        }

        if (!ReferenceEquals(occ, _selected))
        {
            PlaceMan.SetState(new SelectingState(PlaceMan, occ));
        }
    }

    public override void OnHoldStart(IInteractable target, Vector2 screen, Vector3 world)
    {
        if (target is IGridOccupant occ &&
            occ.CanDrag &&
            PlaceMan.GridService != null &&
            PlaceMan.GridService.HasGrid)
        {
            _beltPreviews?.Cleanup();
            _beltPreviews = null;
            _portIndicators?.Cleanup();
            _portIndicators = null;

            PlaceMan.SetState(new DraggingState(PlaceMan, occ, world));
        }
    }

    public override void OnRotateRequested()
    {
        var comp = (_selected as Component);

        var belt = comp?.GetComponent<ConveyorBelt>();
        if (belt != null && _beltPreviews != null)
        {
            _beltPreviews.Cleanup();
            _beltPreviews.ShowOptionsFrom(belt);
        }

        var machine = comp?.GetComponent<Machine>();
        if (machine != null && _portIndicators != null)
        {
            _portIndicators.Cleanup();
            _portIndicators.ShowFor(machine);
        }
    }

    private void RotateAndRefresh(bool clockwise)
    {
        PlaceMan.ExecuteRotateCommand(_selected, clockwise);
        OnRotateRequested();
    }
}
