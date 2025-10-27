using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryDropPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CameraController mainCamera;
    [SerializeField] private GameObject inventoryPanel; // to enable/disable when dragging over drop panel
    [SerializeField] private Button inventoryButton;

    private Button _button;

    public bool IsPointerOver { get; private set; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsPointerOver = true;
        mainCamera.SetInputLocked(false);
        inventoryPanel.SetActive(false);
        inventoryButton.gameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsPointerOver = false;
        if (inventoryPanel != null)
        {
            inventoryButton.gameObject.SetActive(false);
        }
    }
}
