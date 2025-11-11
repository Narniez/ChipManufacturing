using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class Lens : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private KeyCode rotateLeft = KeyCode.Q;
    [SerializeField] private KeyCode rotateRight = KeyCode.E;
    [SerializeField] float rotateSpeed = 60f; //degrees per second
    [SerializeField] Vector3 rotationAxis = Vector3.up;   // rotate around global Y by default
    [SerializeField] bool allowRotation = true;

    [Header("Lens Data")]
    [SerializeField] private Sprite lensSprite;
    [SerializeField] bool isReflective = true;

    //    private LensesController minigameController;

    private void Awake()
    {
        LensesController minigameController = FindAnyObjectByType<LensesController>();
        if (gameObject.activeSelf)
        {
            minigameController.RegisterLens(gameObject);
        }
    }

    private void Start()
    {
    }

    public void DriveRotation(float input, float dt, Space space = Space.World)
    {
        if (!allowRotation || Mathf.Approximately(input, 0f)) return;
        transform.Rotate(rotationAxis, input * rotateSpeed * dt, space);
    }

    private void Reset()
    {
        //Non-trigger collider by default
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = false;
    }
}
    