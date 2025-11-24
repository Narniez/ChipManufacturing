using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class SelectionUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI selectionName;
    [SerializeField] private Button rotateLeftButton;
    [SerializeField] private Button rotateRightButton;
    [SerializeField] private Button testDestroyButton;

    private Action _onRotateLeft;
    private Action _onRotateRight;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);

        if (rotateLeftButton != null)
            rotateLeftButton.onClick.AddListener(() => _onRotateLeft?.Invoke());

        if (rotateRightButton != null)
            rotateRightButton.onClick.AddListener(() => _onRotateRight?.Invoke());
    }

    public void Show(string title, Action onRotateLeft, Action onRotateRight, bool isBelt)
    {
        _onRotateLeft = onRotateLeft;
        _onRotateRight = onRotateRight;
        if (testDestroyButton != null)
        {
            testDestroyButton.gameObject.SetActive(true);
            testDestroyButton.onClick.AddListener(() =>
            {
                var placementManager = PlacementManager.Instance;
                if (placementManager != null)
                {
                    var selectedMachine = placementManager.CurrentSelection as Machine;
                    if (selectedMachine != null)
                    {
                        // Call ReturnMachine to refund the cost
                        EconomyManager.Instance.ReturnMachine(selectedMachine.Data, ref EconomyManager.Instance.playerBalance);
                    }
                    var selectedConveyor = placementManager.CurrentSelection as ConveyorBelt;
                    if(selectedConveyor != null)
                    {
                        // Return conveyor belt cost
                        EconomyManager.Instance.ReturnConveyor(selectedConveyor, ref EconomyManager.Instance.playerBalance);
                    }

                    placementManager.DestroyCurrentSelection();
                }
            });
        }
        if (selectionName != null) selectionName.text = string.IsNullOrEmpty(title) ? "Selected" : title;
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        _onRotateLeft = null;
        _onRotateRight = null;
        if (panel != null) panel.SetActive(false);
    }
}
