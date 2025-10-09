using UnityEngine;

public class ConveyorItem 
{
    public MaterialType Material;
    // 0..1 progress across the current belt if you later want smooth lerp
    public float MoveProgress;

    public ConveyorItem(MaterialType mat)
    {
        Material = mat;
        MoveProgress = 0f;
    }
}
