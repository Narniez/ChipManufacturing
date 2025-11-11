using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BrokenMachineIndicator : MonoBehaviour, IInteractable
{
    private Machine _machine;
    private BrokenMachineManager _manager;

    // Optional: billboard
    [SerializeField] private bool _faceCamera = true;

    public void Attach(Machine machine, BrokenMachineManager manager)
    {
        _machine = machine;
        _manager = manager;
    }

    private void LateUpdate()
    {
        if (_faceCamera && Camera.main != null)
        {
            var camFwd = Camera.main.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(camFwd);
        }
    }

    public void OnTap()
    {
        if (_manager != null && _machine != null)
            _manager.OpenRepairUI(_machine);
    }

    public void OnHold() { }
}