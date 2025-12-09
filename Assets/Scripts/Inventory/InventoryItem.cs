using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler/*, IPointerClickHandler*/
{
    [Header("Bound Data")]
    [SerializeField] private MaterialData slotItem;
    [SerializeField] private int slotQuantity = 0;

    [Header("UI")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private SellPopup sellPopup;

    [Header("Drag")]
    [SerializeField] private CanvasGroup canvasGroup;

    private Canvas _canvas;
    private RectTransform _rt;
    private Transform _originalParent;
    private bool _dropHandledThisDrag;

    public InventorySlot CurrentSlot { get; private set; }
    private InventorySlot _originSlot;
    public MaterialData SlotItem => slotItem;
    public int SlotQuantity => slotQuantity;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        if (canvasGroup == null) canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (CurrentSlot == null)
            CurrentSlot = GetComponentInParent<InventorySlot>();
        _originalParent = transform.parent;
        SetStats();

        _canvas = GetComponentInParent<Canvas>();

    }

    private void Start()
    {
    }

    public void SetCurrentSlot(InventorySlot slot)
    {
        CurrentSlot = slot;
        if (_originSlot == null) _originSlot = slot; // latch origin
        
    }

    public void Setup(MaterialData data, int quantity)
    {
        slotItem = data;
        slotQuantity = Mathf.Max(0, quantity);
        SetStats();
    }

    public void SetStats()
    {
        bool hasData = slotItem != null && slotQuantity > 0;
        if (itemIcon)
        {
            itemIcon.sprite = hasData ? slotItem.icon : null;
            itemIcon.enabled = true;
        }
        if (quantityText)
        {
            quantityText.text = hasData ? slotQuantity.ToString() : "";
            quantityText.color = hasData ? Color.white : new Color(1, 1, 1, 0.15f);
        }
    }

    public void RefreshFromService()
    {
        if (slotItem == null || InventoryService.Instance == null) return;
        slotQuantity = InventoryService.Instance.GetCount(slotItem.id);
        SetStats();
    }


    // ---------------- DRAG / DROP ----------------

    public void OnBeginDrag(PointerEventData e)
    {
        if (slotItem == null || slotQuantity <= 0) return;

        if (sellPopup && sellPopup.gameObject.activeSelf) sellPopup.Close();

        _dropHandledThisDrag = false;
        _originalParent = transform.parent;

        // Reparent this ITEM to the top canvas so it follows the pointer smoothly
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        transform.SetParent(_canvas.transform, true); // keep world pos
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.7f;
    }

    public void OnDrag(PointerEventData e)
    {
        _rt.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        bool worldConsumed = false;

        // --- Unlock input if dropped on game view (not UI) ---
        if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
        {
            // Raycast into the world to find a machine
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var machine = hit.collider.GetComponentInChildren<Machine>();

              
                if (machine != null)
                {
                    Debug.Log($"Dropped item on machine: {machine.name}");
                    int used = machine.TryQueueInventoryItem(slotItem, slotQuantity);
                    if (used > 0)
                    {
                        Debug.Log("took item");
                        worldConsumed = true;
                        // Update global inventory
                        if (InventoryService.Instance != null)
                            InventoryService.Instance.TryRemove(slotItem.id, used);

                        // Reflect consumption in the ORIGIN slot UI (so remainder goes back)
                        if (_originSlot != null)
                            _originSlot.AddAmount(-used);
                        Debug.Log("item no more in the inventory");

                        // Proxy is only a drag visual — remove it whether partial or full
                        Destroy(gameObject);
                        //CurrentSlot.Clear();
                                           
                    }
                }
            }

        }

        if (!_dropHandledThisDrag && !worldConsumed)
        {
            Destroy(gameObject);
            
        }

        // Always unlock the camera at the end, no matter what path we took
        var cam = Camera.main;
        if (cam != null)
        {
            var camController = cam.GetComponent<CameraController>();
            if (camController != null) camController.SetInputLocked(false);
        }
    }

    public void NotifyDroppedHandled()
    {
        _dropHandledThisDrag = true;
        (_originSlot ?? CurrentSlot)?.Clear();
    }

   /* public void OnPointerClick(PointerEventData eventData)
    {
        if (sellPopup == null) return;

        if (sellPopup.gameObject.activeSelf)
            sellPopup.Close();
        else
            sellPopup.OpenFor(this);
    }*/
}
