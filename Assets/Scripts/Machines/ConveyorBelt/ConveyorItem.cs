using UnityEngine;

public class ConveyorItem 
{
    public MaterialData materialData;
    public GameObject Visual;

    // Smooth hop animation state
    public Vector3 From;
    public Vector3 To;
    public float smoothTime;         // 0..1
    public float Duration;  // seconds

    public ConveyorItem(MaterialData mat, GameObject visual = null)
    {
        materialData = mat;
        Visual = visual;
        smoothTime = 0.5f;
        Duration = 0f;
    }

    public void BeginMove(Vector3 from, Vector3 to, float duration)
    {
        From = from;
        To = to;
        Duration = Mathf.Max(0.0001f, duration);
        smoothTime = 0f;
        if (Visual != null) Visual.transform.position = from;
    }

    // Returns true while animating, false when finished or no visual
    public bool Animate(float dt)
    {
        if (Visual == null || Duration <= 0f || smoothTime >= 1f) return false;
        smoothTime = Mathf.Min(1f, smoothTime + dt / Duration);
        Visual.transform.position = Vector3.Lerp(From, To, smoothTime);
        return smoothTime < 1f;
    }
}
