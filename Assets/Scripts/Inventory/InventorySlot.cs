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
    [Header("Debug")]
    [SerializeField] private bool debug = true;

    [Header("UI")]
    [Tooltip("Icon image to display the item's sprite")]
    [SerializeField] private Image icon;
    [Tooltip("Text to display the stack count")]
    [SerializeField] private TextMeshProUGUI amountText;

    [SerializeField] private SellPopup sellPopup;

    [Header("Drag")]
    [SerializeField] private CanvasGroup canvasGroup;


    // Backing data for this slot
    public MaterialData Item { get; private set; }
    public int Amount { get; private set; }
    public bool IsEmpty => Item == null || Amount <= 0;

    private Sprite _slotSprite;
    private TextMeshProUGUI _itemAmount;

    private bool _dragging;
    private bool _dropHandledThisDrag;

    private Canvas _canvas;
    private GameObject _dragGhost;
    private RectTransform _dragGhostRt;
    private Image _dragGhostIcon;
    private TextMeshProUGUI _dragGhostAmount;
    private Color _originalIconColor;
    private Color _slotColor;

    //private Transform _originalParent;

    private void Awake()
    {
        var img = GetComponent<Image>();
        img.raycastTarget = true;

        if (canvasGroup == null) canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;

        // Cache original icon sprite/color so we can restore them when the slot is empty
        if (icon != null)
        {
            _slotSprite = icon.sprite;
            _slotColor = icon.color;
        }

        //UpdateUi();
    }

    public void PlaceItem(InventoryItem item)
    {
        if (item == null) return;

        // Copy data from the InventoryItem, then destroy it.
        SetItem(item.SlotItem, item.SlotQuantity);

        Destroy(item.gameObject);
    }

    // Directly assign material + amount
    public void SetItem(MaterialData mat, int amount)
    {
        Item = mat;
        Amount = Mathf.Max(0, amount);
        if (Amount == 0) Item = null;

        UpdateUI();
    }

    public void AddAmount(int delta)
    {
        if (IsEmpty && delta <= 0) return;
        Amount += delta;
        if (Amount <= 0) Clear();
        else UpdateUI();        
    }

    public void Clear()
    {
        Item = null;
        Amount = 0;
        UpdateUI();
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
                icon.enabled = true;
                icon.sprite = _slotSprite;
                icon.color = _slotColor;
            }
        }

        if (amountText != null)
        {
            amountText.text = !IsEmpty ? Amount.ToString() : string.Empty;
        }
    }

    // ---------------- DRAG & DROP ----------------
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty) return;

        if (sellPopup && sellPopup.gameObject.activeSelf)
            sellPopup.Close();

        _dropHandledThisDrag = false;
        _dragging = true;

        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) return;

        // Let events pass through this slot while dragging
        canvasGroup.blocksRaycasts = false;

        // Create drag ghost
        CreateDragGhost();

        if (icon != null)
        {
            _originalIconColor = icon.color;
            var color = icon.color; color.a = 0.35f;
            icon.color = color;
        }

        SetGhostPosition(eventData);
    }
      

    public void OnDrag(PointerEventData eventData)
    {
       if(!_dragging || _dragGhostRt == null) return;
       SetGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;

        if (icon != null) icon.color = _originalIconColor;

        // IMPORTANT: resolve hovered target BEFORE re-enabling raycasts on the source slot
        GameObject hoveredGO = eventData.pointerCurrentRaycast.gameObject != null
            ? eventData.pointerCurrentRaycast.gameObject
            : eventData.pointerEnter;

        var targetSlot = hoveredGO ? hoveredGO.GetComponentInParent<InventorySlot>() : null;

        // Now restore raycasts on the source
        canvasGroup.blocksRaycasts = true;

        if (targetSlot != null && targetSlot != this)
        {
            // Stack if same item id; else swap (move if target is empty)
            if (!targetSlot.IsEmpty && targetSlot.Item != null && Item != null && targetSlot.Item.id == Item.id)
            {
                targetSlot.AddAmount(Amount);
                Clear();                 // calls UpdateUI()
                if (debug) Debug.Log($"[InventorySlot] Stacked {Amount} onto {targetSlot.name}");
            }
            else
            {
                var tmpItem = targetSlot.Item;
                var tmpAmt  = targetSlot.Amount;

                targetSlot.SetItem(Item, Amount); // updates target visuals
                SetItem(tmpItem, tmpAmt);         // updates source visuals

                if (debug) Debug.Log($"[InventorySlot] Swapped {name} <-> {targetSlot.name}");
            }
        }

        DestroyDragGhost();
        _dropHandledThisDrag = true;
    }

    public void OnDrop(PointerEventData eventData)
    {
        /* // Accept drops of InventoryItem: copy its data into this slot and destroy it
         var dragged = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<InventoryItem>() : null;
         if (dragged == null) return;

         // If same item, stack; else overwrite (or add your swap logic)
         if (!IsEmpty && Item == dragged.SlotItem)
         {
             AddAmount(dragged.SlotQuantity);
             if (InventoryService.Instance != null)
                 InventoryService.Instance.TryApply(new Dictionary<int, int> { { dragged.SlotItem.id, dragged.SlotQuantity } });
             Destroy(dragged.gameObject);
             return;
         }

         PlaceItem(dragged);*/

        var dragged = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<InventoryItem>() : null;
        if (dragged == null) return;

        if (!IsEmpty && Item != null && dragged.SlotItem != null && Item.id == dragged.SlotItem.id)
        {
            AddAmount(dragged.SlotQuantity);
            Destroy(dragged.gameObject);
            return;
        }

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
        }
    }
          
    private void CreateDragGhost()
    {
        DestroyDragGhost();

        _dragGhost = new GameObject("SlotDragGhost", typeof(RectTransform));
        _dragGhost.transform.SetParent(_canvas.transform, false);
        _dragGhost.transform.SetAsLastSibling();

        _dragGhostRt = _dragGhost.GetComponent<RectTransform>();
        //_dragGhostRt.sizeDelta = (icon != null && icon.sprite != null) ? new Vector2(icon.sprite.rect.width, icon.sprite.rect.height) : new Vector2(64, 64);

        _dragGhostIcon = _dragGhost.AddComponent<Image>();
        _dragGhostIcon.raycastTarget = false;
        _dragGhostIcon.sprite = icon != null ? icon.sprite : null;
        _dragGhostIcon.color = Color.white;

        // Amount label
        var amountGO = new GameObject("Amount", typeof(RectTransform));
        amountGO.transform.SetParent(_dragGhost.transform, false);
        var amountRT = amountGO.GetComponent<RectTransform>();
        amountRT.anchorMin = new Vector2(1, 0);
        amountRT.anchorMax = new Vector2(1, 0);
        amountRT.pivot = new Vector2(1, 0);
        amountRT.anchoredPosition = new Vector2(-8, 8);

        _dragGhostAmount = amountGO.AddComponent<TextMeshProUGUI>();
        _dragGhostAmount.raycastTarget = false;
        _dragGhostAmount.fontSize = 24;
        _dragGhostAmount.alignment = TextAlignmentOptions.BottomRight;
        _dragGhostAmount.text = Amount > 1 ? Amount.ToString() : "1";
        _dragGhostAmount.color = Color.white;
        _dragGhostAmount.enableAutoSizing = true;
   
    }

    private void DestroyDragGhost()
    {
        if (_dragGhost != null) Destroy(_dragGhost);
        _dragGhost = null;
        _dragGhostRt = null;
        _dragGhostIcon = null;
        _dragGhostAmount = null;
    }

    private void SetGhostPosition(PointerEventData eventData)
    {
        if (_dragGhostRt == null) return;
        Vector3 globalMousePos;
        RectTransform plane = _canvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(plane, eventData.position, eventData.pressEventCamera, out globalMousePos))
        {
            _dragGhostRt.position = globalMousePos;
            _dragGhostRt.rotation = plane.rotation;
        }
        else
        {   
            _dragGhostRt.position = eventData.position;
        }
    }

}
