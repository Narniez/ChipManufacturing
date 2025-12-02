using UnityEngine;

public class ClawController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float boundaryX = 7f;

    // vertical swinging parameters
    [SerializeField] private float yAmplitude = 0.5f;
    [SerializeField] private float ySpeed = 1f;

    private Transform clawTransform;
    private float initialY;

    private void Start()
    {
        clawTransform = transform;
        initialY = clawTransform.position.y;
    }

    private void FixedUpdate()
    {
        // horizontal Z back-and-forth motion between -boundaryX and +boundaryX
        float z = Mathf.PingPong(Time.time * moveSpeed, boundaryX * 2f) - boundaryX;

        // vertical Y swinging motion
        float y = initialY + Mathf.Sin(Time.time * ySpeed) * yAmplitude;

        clawTransform.position = new Vector3(clawTransform.position.x, y, z);
    }
}
