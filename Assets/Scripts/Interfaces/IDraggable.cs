using UnityEngine;

public interface IDraggable
{
    // Whether this object can be dragged right now
    bool CanDrag { get; }

    // Which transform should be moved while dragging (usually the root)
    Transform DragTransform { get; }

   
    void OnDragStart();
    void OnDrag(Vector3 worldPosition);
    void OnDragEnd();
}
