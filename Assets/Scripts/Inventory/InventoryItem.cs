using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Bound Data")]
    [SerializeField] private MaterialData slotItem;  
    [SerializeField] private int slotQuantity = 0;

    [Header("UI")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;

    [Header("Drag")]
    [SerializeField] private Canvas dragCanvasOverride;
    [SerializeField] private CanvasGroup canvasGroup;

    private Canvas _canvas;
    private RectTransform _rt;
    private Transform _originalParent;
    private bool _dropHandledThisDrag;

    public InventorySlot CurrentSlot { get; private set; }
    public MaterialData SlotItem => slotItem;
    public int SlotQuantity => slotQuantity;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        if (canvasGroup == null) canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        // Find parent slot & canvas
        CurrentSlot = GetComponentInParent<InventorySlot>();
        _originalParent = transform.parent;

        _canvas = dragCanvasOverride != null ? dragCanvasOverride : GetComponentInParent<Canvas>();
        SetStats();
    }

    public void SetCurrentSlot(InventorySlot slot) => CurrentSlot = slot;

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
     // ---------------- DRAG / DROP ----------------

    public void OnBeginDrag(PointerEventData e)
    {
        if (slotItem == null || slotQuantity <= 0) return;

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

        if (!_dropHandledThisDrag)
        {
            transform.SetParent(_originalParent, false);
            var rt = (RectTransform)transform;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;

            // Also rebind CurrentSlot just in case
            var slot = _originalParent ? _originalParent.GetComponentInParent<InventorySlot>() : null;
            if (slot != null) CurrentSlot = slot;
        }
    }
    public void NotifyDroppedHandled() => _dropHandledThisDrag = true;
}
