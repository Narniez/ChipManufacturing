using UnityEngine;

public class SelectingState : BasePlacementState
{
    private readonly IGridOccupant _selected;
    private BeltChainPreviewController _beltPreviews;

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

        // Wrap rotate actions so previews update immediately
        ui.Show(
            name,
            onRotateLeft:  () => RotateAndRefresh(clockwise: false),
            onRotateRight: () => RotateAndRefresh(clockwise: true)
        );

        // If selecting a belt, show extend previews
        var belt = comp.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            _beltPreviews = new BeltChainPreviewController(PlaceMan);
            _beltPreviews.ShowOptionsFrom(belt);
        }
    }

    public override void Exit()
    {
        PlaceMan.SelectionUI?.Hide();

        if (_beltPreviews != null)
        {
            _beltPreviews.Cleanup();
            _beltPreviews = null;
        }
    }

    public override void OnTap(IInteractable target, Vector2 screen, Vector3 world)
    {
        // Tap a preview -> place and continue chaining
        var prev = (target as Component)?.GetComponent<ConveyorPreview>();
        if (prev != null && _beltPreviews != null)
        {
            var placed = _beltPreviews.PlaceFromPreview(prev);
            _beltPreviews.Cleanup();

            if (placed != null)
            {
                // Continue chain from newly placed belt by re-entering selection
                PlaceMan.SetState(new SelectingState(PlaceMan, placed));
                return;
            }
        }

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
        // Tap same occupant -> keep selection
    }

    public override void OnHoldStart(IInteractable target, Vector2 screen, Vector3 world)
    {
        // Begin dragging from selection
        if (target is IGridOccupant occ &&
            occ.CanDrag &&
            PlaceMan.GridService != null &&
            PlaceMan.GridService.HasGrid)
        {
            // Clear previews before dragging
            if (_beltPreviews != null)
            {
                _beltPreviews.Cleanup();
                _beltPreviews = null;
            }

            PlaceMan.SetState(new DraggingState(PlaceMan, occ, world));
        }
    }

    public override void OnRotateRequested()
    {
        // Keyboard/gesture rotate path -> refresh previews
        var comp = (_selected as Component);
        var belt = comp?.GetComponent<ConveyorBelt>();
        if (belt != null && _beltPreviews != null)
        {
            _beltPreviews.Cleanup();
            _beltPreviews.ShowOptionsFrom(belt);
        }
    }

    private void RotateAndRefresh(bool clockwise)
    {
        PlaceMan.ExecuteRotateCommand(_selected, clockwise);

        var comp = (_selected as Component);
        var belt = comp?.GetComponent<ConveyorBelt>();
        if (belt != null && _beltPreviews != null)
        {
            _beltPreviews.Cleanup();
            _beltPreviews.ShowOptionsFrom(belt);
        }
    }
}
