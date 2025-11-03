using System;
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

        // Only bail if nothing is selected
        if (_selected == null) return;

        var comp = (_selected as Component);

        string name = ResolveDisplayName(comp);

        bool isBelt = comp?.GetComponent<ConveyorBelt>() != null;

        ui.Show(
            name,
            onRotateLeft:  () => RotateAndRefresh(clockwise: false),
            onRotateRight: () => RotateAndRefresh(clockwise: true),
            isBelt
        );

        // Show UI only if available
        var ui = PlaceMan.SelectionUI;
        if (ui != null)
        {
            ui.Show(
                name,
                onRotateLeft:  () => RotateAndRefresh(clockwise: false),
                onRotateRight: () => RotateAndRefresh(clockwise: true),
                isBelt
            );
        }

        // Always show belt preview options for belts, regardless of SelectionUI presence
        var belt = comp?.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            _beltPreviews = new BeltChainPreviewController(PlaceMan);
            _beltPreviews.ShowOptionsFrom(belt);
        }

        // Same for machine port indicators
        var machine = comp?.GetComponent<Machine>();
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
            // Place the belt at the tapped preview cell
            var placed = _beltPreviews.PlaceFromPreview(prev);

            if (placed != null)
            {
                PlaceMan.SetState(new SelectingState(PlaceMan, placed));
                return;
            }

            // If placement failed, refresh the current previews from the existing selected belt
            _beltPreviews.Cleanup();
            var currentBelt = (_selected as Component)?.GetComponent<ConveyorBelt>();
            if (currentBelt != null)
                _beltPreviews.ShowOptionsFrom(currentBelt);

            return;
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

    private static string ResolveDisplayName(Component comp)
    {
        if (comp == null) return string.Empty;

        var machine = comp.GetComponent<Machine>();
        if (machine?.Data != null && !string.IsNullOrEmpty(machine.Data.machineName))
            return machine.Data.machineName;

        if (comp.GetComponent<ConveyorBelt>() != null)
            return "Conveyor Belt";

        return StripClone(comp.name);
    }

    private static string StripClone(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        const string suffix = "(Clone)";
        if (name.EndsWith(suffix, StringComparison.Ordinal))
            return name.Substring(0, name.Length - suffix.Length).TrimEnd();
        return name;
    }
}
