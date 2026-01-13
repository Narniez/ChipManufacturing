using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(UnityEngine.UI.Image))]
public class InventorySlot : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    /*[Header("Debug")]
    [SerializeField] private bool debug = true;*/

    [Header("UI")]
    [Tooltip("Icon image to display the item's sprite")]
    [SerializeField] private Image icon;
    [Tooltip("Text to display the stack count")]
    [SerializeField] private TextMeshProUGUI amountText;
    [Tooltip("Item Name")]
    [SerializeField] private TextMeshProUGUI nameText;

    [SerializeField] private SellPopup sellPopup;


    [Header("Drag")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Canvas dragCanvas;

    [SerializeField] private InventoryItem inventoryItemPrefab;
    private GameObject _activeProxy;
    private NewCameraControls mainCamera;


    // backing data for this slot
    public MaterialData Item { get; private set; }
    public int Amount { get; private set; }
    public bool IsEmpty => Item == null || Amount <= 0;

    //private Sprite _slotSprite;
  
    private Color _slotColor;

    [Tooltip("Sprite to use when the slot is empty (background frame).")]
    [SerializeField] private Sprite emptySlotSprite;


    private void Awake()
    {
        var img = GetComponent<Image>();
        img.raycastTarget = true;

        if (canvasGroup == null) canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;

        dragCanvas = GameObject.FindGameObjectWithTag("PopupCanvas")?.GetComponent<Canvas>();

        // caching original icon sprite/color so we can restore them when the slot is empty
        if (icon != null)
        {
            //_slotSprite = img.sprite;
            _slotColor = icon.color;
        }

        mainCamera = GameObject.FindGameObjectWithTag("MainCamera")?.GetComponent<NewCameraControls>();

        //UpdateUi();
    }

    public void PlaceItem(InventoryItem item)
    {
        if (item == null) return;

        // copying data from the InventoryItem, then destroting it
        SetItem(item.SlotItem, item.SlotQuantity);

        Destroy(item.gameObject);
    }

    // Directly assigns material + amount
    public void SetItem(MaterialData mat, int amount)
    {
        // computes previous values to derive inventory delta
        MaterialData prevItem = Item;
        int prevAmount = Amount;

        // applying new values
        Item = mat;
        Amount = Mathf.Max(0, amount);
        if (Amount == 0) Item = null;

        UpdateUI();

        // updating InventoryService totals (unless we're loading/restoring state)
        if (InventoryService.Instance != null && !InventoryService.Instance.IsLoading)
        {
            var delta = new Dictionary<int, int>();

            if (prevItem != null)
                delta[prevItem.id] = delta.TryGetValue(prevItem.id, out var v1) ? v1 - prevAmount : -prevAmount;

            if (mat != null)
                delta[mat.id] = delta.TryGetValue(mat.id, out var v2) ? v2 + Amount : Amount;

            var clean = new Dictionary<int, int>();
            foreach (var kv in delta) if (kv.Value != 0) clean[kv.Key] = kv.Value;

            if (clean.Count > 0)
                InventoryService.Instance.TryApply(clean);
        }
    }

    public void AddAmount(int delta)
    {
        if (IsEmpty && delta <= 0) return;
        MaterialData prevItem = Item;
        int prevAmount = Amount;

        Amount += delta;
        if (Amount <= 0) Clear();
        else UpdateUI();

        if (InventoryService.Instance != null && !InventoryService.Instance.IsLoading && prevItem != null && delta != 0)
        {
            InventoryService.Instance.TryApply(new Dictionary<int, int> { { prevItem.id, delta } });
        }
    }

    public void Clear()
    {
        if (Item == null && Amount == 0) return;

        MaterialData prevItem = Item;
        int prevAmount = Amount;

        icon.sprite = emptySlotSprite;
        Item = null;
        Amount = 0;
        UpdateUI();

        if (InventoryService.Instance != null && !InventoryService.Instance.IsLoading && prevItem != null && prevAmount > 0)
        {
            InventoryService.Instance.TryApply(new Dictionary<int, int> { { prevItem.id, -prevAmount } });
        }
    }

    private static string GetDisplayName(MaterialData mat)
    {
        if (mat == null) return string.Empty;
        // Prefer explicit materialName; fallback to asset name
        return !string.IsNullOrWhiteSpace(mat.materialName) ? mat.materialName : mat.name;
    }

    private void UpdateUI()
    {
        if (icon != null)
        {
            if (!IsEmpty && Item.icon != null)
            {
                icon.enabled = true;
                icon.sprite = Item.icon;
                icon.color = Color.white;
            }
            else
            {
                // Restore original icon sprite & color when slot is empty
                //icon.enabled = true;
                icon.sprite = emptySlotSprite;
                icon.color = _slotColor;
            }
        }

        if (amountText != null)
        {
            amountText.text = !IsEmpty ? Amount.ToString() : string.Empty;
        }

        if (nameText != null)
        {
            nameText.text = !IsEmpty ? GetDisplayName(Item) : string.Empty;
        }
    }

    // ---------------- DRAG & DROP ----------------
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty) return;

        if (sellPopup && sellPopup.gameObject.activeSelf)
            sellPopup.Close();

        mainCamera.SetInputLocked(true);

        var parent = canvasGroup ? canvasGroup.transform : (GetComponentInParent<Canvas>()?.transform ?? transform.root);
        //var parent = dragCanvas ? dragCanvas.transform : (GetComponentInParent<Canvas>()?.transform ?? transform.root);
        var proxy = Instantiate(inventoryItemPrefab, parent, false);
        proxy.Setup(Item, Amount);
        proxy.SetCurrentSlot(this);
        proxy.transform.position = eventData.position;

        _activeProxy = proxy.gameObject;

        // hand off the drag to the proxy
        eventData.pointerDrag = _activeProxy;
        ExecuteEvents.Execute<IBeginDragHandler>(_activeProxy, eventData, ExecuteEvents.beginDragHandler);
    }


    public void OnDrag(PointerEventData e)
    {
        if (_activeProxy != null)
        {
           // mainCamera.SetInputLocked(true);
            ExecuteEvents.Execute<IDragHandler>(_activeProxy, e, ExecuteEvents.dragHandler);
            return;
        }
    }

    private void OnDisable() { if (icon) icon.color = _slotColor; _activeProxy = null; }
    private void OnDestroy() { if (icon) icon.color = _slotColor; _activeProxy = null; }

    public void OnEndDrag(PointerEventData e)
    {
        if (_activeProxy != null)
        {
            mainCamera.SetInputLocked(false);
            ExecuteEvents.Execute<IEndDragHandler>(_activeProxy, e, ExecuteEvents.endDragHandler);
            _activeProxy = null;
        }

        if (icon) icon.color = _slotColor; // restore
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<InventoryItem>() : null;
        if (dragged == null || dragged.CurrentSlot == this) return;

        if (!IsEmpty && Item != null && dragged.SlotItem != null && Item.id == dragged.SlotItem.id)
        {
            AddAmount(dragged.SlotQuantity);
            dragged.NotifyDroppedHandled();
            Destroy(dragged.gameObject);
            return;
        }

        if (!IsEmpty && Item != null && dragged.SlotItem != null && Item.id != dragged.SlotItem.id)
        {
            var origin = dragged.CurrentSlot;     // origin slot (latched earlier)
            var prevItem = Item;                  // A
            var prevAmount = Amount;

            // target becomes dragged B
            SetItem(dragged.SlotItem, dragged.SlotQuantity);

            // origin becomes previous A
            if (origin != null)
                origin.SetItem(prevItem, prevAmount);

            Destroy(dragged.gameObject);          
            return;
        }
        dragged.NotifyDroppedHandled();
        PlaceItem(dragged);
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        if (sellPopup == null) return;

        if (!IsEmpty)
        {
            if (sellPopup.gameObject.activeSelf)
                sellPopup.Close();
            else
                sellPopup.OpenForSlot(this);
                TutorialEventBus.PublishInventoryItemSelected();
        }
    }
          
}
