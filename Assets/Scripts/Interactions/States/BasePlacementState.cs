using UnityEngine;

public abstract class BasePlacementState : IPlacementState
{
    protected readonly PlacementManager PlaceMan;

    protected BasePlacementState(PlacementManager plM) => PlaceMan = plM;

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update() { }
    public virtual void OnTap(IInteractable target, Vector2 screen, Vector3 world) { }
    public virtual void OnHoldStart(IInteractable target, Vector2 screen, Vector3 world) { }
    public virtual void OnHoldMove(IInteractable target, Vector2 screen, Vector3 world) { }
    public virtual void OnHoldEnd(IInteractable target, Vector2 screen, Vector3 world) { }
    public virtual void OnRotateRequested() { }
}
