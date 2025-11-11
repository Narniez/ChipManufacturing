using UnityEngine;

public class BrokenMachineUI : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;

    private Machine _current;

    public void OpenFor(Machine machine)
    {
        _current = machine;
        if (_panelRoot != null) _panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (_panelRoot != null) _panelRoot.SetActive(false);
        _current = null;
    }

    public bool IsOpenFor(Machine machine) => _current == machine;

    // Hook to a UI Button
    public void OnRepairButton()
    {
        if (_current == null) return;
        BrokenMachineManager.Instance?.Repair(_current);
        Close();
    }
}