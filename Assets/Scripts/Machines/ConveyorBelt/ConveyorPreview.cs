using UnityEngine;

public class ConveyorPreview : MonoBehaviour, IInteractable
{
    // Target cell to place the next belt
    public Vector2Int Cell;
    // The orientation the placed belt should have
    public GridOrientation Orientation;
    // Whether the piece to place is a turn (vs straight)
    public bool IsTurn;

    // IInteractable: no internal action; SelectingState handles the tap
    public void OnTap() { }
    public void OnHold() { }
}
