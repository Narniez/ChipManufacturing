using UnityEngine;

public class ConveyorItem 
{
    public MaterialType Material;
    // 0..1 progress across the current belt if you later want smooth lerp
    public float MoveProgress;
    public GameObject Visual;

    public ConveyorItem(MaterialType mat, GameObject visual = null)
    {
        Material = mat;
        MoveProgress = 0f;
        Visual = visual;
    }
}
