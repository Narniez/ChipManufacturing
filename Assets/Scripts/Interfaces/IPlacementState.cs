using UnityEngine;

public interface IPlacementState
{
    void Enter();
    void Exit();
    void Update();
    void OnTap(IInteractable target, Vector2 screen, Vector3 world);
    void OnHoldStart(IInteractable target, Vector2 screen, Vector3 world);
    void OnHoldMove(IInteractable target, Vector2 screen, Vector3 world);
    void OnHoldEnd(IInteractable target, Vector2 screen, Vector3 world);
    void OnRotateRequested();
}
