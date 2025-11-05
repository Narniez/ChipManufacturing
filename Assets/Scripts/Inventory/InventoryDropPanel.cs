using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryDropPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CameraController mainCamera;
    [SerializeField] private GameObject inventoryPanel; // to enable/disable when dragging over drop panel
    [SerializeField] private Button inventoryButton;

    [SerializeField] private CanvasGroup dropPanelCanvasGroup;

    private bool _lockedByDrag;   // track if *we* locked the camera due to a drag

    public bool IsPointerOver { get; private set; }

    private void Awake()
    {
        if (dropPanelCanvasGroup == null)
        {
            dropPanelCanvasGroup = GetComponent<CanvasGroup>();
            if (dropPanelCanvasGroup == null) dropPanelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        dropPanelCanvasGroup.blocksRaycasts = true;  // default: blocks
    }


    /* public void OnPointerEnter(PointerEventData eventData)
     {
         IsPointerOver = true;
         mainCamera.SetInputLocked(false);
         inventoryPanel.SetActive(false);
         inventoryButton.gameObject.SetActive(true);
     }*/

    /*public void OnPointerExit(PointerEventData eventData)
    {
        IsPointerOver = false;
        if (mainCamera != null) mainCamera.SetInputLocked(false);

        if (dropPanelCanvasGroup != null) dropPanelCanvasGroup.blocksRaycasts = true;

        if (inventoryButton != null) inventoryButton.gameObject.SetActive(false);

    }*/

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsPointerOver = true;
        var dragged = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<InventoryItem>() : null;
        bool draggingInventoryItem = dragged != null;

        if (draggingInventoryItem)
        {
            // lock camera only during an inventory-item drag over the panel
            if (mainCamera) mainCamera.SetInputLocked(true);
            _lockedByDrag = true;

            // optional UI toggles while in "drop mode"
            if (inventoryPanel) inventoryPanel.SetActive(false);
            if (inventoryButton) inventoryButton.gameObject.SetActive(true);

            // let world receive the drop even though we're over UI
            if (dropPanelCanvasGroup) dropPanelCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            IsPointerOver = true;
            dropPanelCanvasGroup.blocksRaycasts = true;
            mainCamera.SetInputLocked(false);
            inventoryPanel.SetActive(false);
            inventoryButton.gameObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsPointerOver = false;
        if (mainCamera != null) mainCamera.SetInputLocked(false);
        if (dropPanelCanvasGroup) dropPanelCanvasGroup.blocksRaycasts = true;
        if (inventoryButton) inventoryButton.gameObject.SetActive(false);

        // unlock only if we were the ones who locked it (i.e., we were in a drag)
        if (_lockedByDrag && mainCamera) mainCamera.SetInputLocked(false);
        _lockedByDrag = false;
    }
}

