using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BrokenMachineUI : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Button _startMinigameButton;
    [SerializeField] private Button _instantRepairButton;  // optional fallback if no minigame
    [SerializeField] private Button _closeButton;

    private Machine _current;

    public void OpenFor(Machine machine)
    {
        _current = machine;
        if (_panelRoot != null) _panelRoot.SetActive(true);

        var hasMinigame = machine.Data != null && machine.Data.repairMinigame != null;

        if (_titleText != null)
        {
            string mgName = hasMinigame ? machine.Data.repairMinigame.displayName : "Broken Machine";
            _titleText.text = mgName;
        }

        if (_startMinigameButton != null)
        {
            _startMinigameButton.gameObject.SetActive(hasMinigame);
            _startMinigameButton.onClick.RemoveAllListeners();
            if (hasMinigame)
                _startMinigameButton.onClick.AddListener(OnStartMinigame);
        }

        if (_instantRepairButton != null)
        {
            _instantRepairButton.gameObject.SetActive(!hasMinigame);
            _instantRepairButton.onClick.RemoveAllListeners();
            _instantRepairButton.onClick.AddListener(OnRepairButton); 
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(Close);
        }
    }

    public void Close()
    {
        if (_panelRoot != null) _panelRoot.SetActive(false);
        _current = null;
    }

    public bool IsOpenFor(Machine machine) => _current == machine;

    // Fallback direct repair (no minigame)
    public void OnRepairButton()
    {
        if (_current == null) return;
        BrokenMachineManager.Instance?.Repair(_current);
        Close();
    }

    // Launch minigame via RepairMinigameManager
    private void OnStartMinigame()
    {
        if (_current == null) return;
        var def = _current.Data.repairMinigame;
        if (def == null)
        {
            // Should not happen if button is hidden correctly
            OnRepairButton();
            return;
        }

        string returnScene = SceneManager.GetActiveScene().name;
        RepairMinigameManager.Begin(_current, def, returnScene);
        Close();
    }
}