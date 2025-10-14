using UnityEngine;

public class ConveyorItem 
{
    public MaterialType Material;
    public GameObject Visual;

    // Smooth hop animation state
    public Vector3 From;
    public Vector3 To;
    public float T;         // 0..1
    public float Duration;  // seconds

    public ConveyorItem(MaterialType mat, GameObject visual = null)
    {
        Material = mat;
        Visual = visual;
        T = 1f;
        Duration = 0f;
    }

    public void BeginMove(Vector3 from, Vector3 to, float duration)
    {
        From = from;
        To = to;
        Duration = Mathf.Max(0.0001f, duration);
        T = 0f;
        if (Visual != null) Visual.transform.position = from;
    }

    // Returns true while animating, false when finished or no visual
    public bool Animate(float dt)
    {
        if (Visual == null || Duration <= 0f || T >= 1f) return false;
        T = Mathf.Min(1f, T + dt / Duration);
        Visual.transform.position = Vector3.Lerp(From, To, T);
        return T < 1f;
    }
}
