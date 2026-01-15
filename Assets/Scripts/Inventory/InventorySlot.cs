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
    // --- Global button references (assign in Inspector on one slot only) ---
    [Header("Global Sell Controls")]
    [SerializeField] private Button plusButton;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button minButton;
    [SerializeField] private Button maxButton;
    [SerializeField] private Button sellButton;

    // Static shared buttons (wired once)
    private static bool _buttonsWired;
    private static Button _PLUS, _MINUS, _MIN, _MAX, _SELL;

    // Track the currently selected slot in this panel instance
    private static InventorySlot _currentSelected;

    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI nameText;
    [Tooltip("Color overlay applied to the icon when this slot is selected.")]
    [SerializeField] private Color selectedHighlight = new Color(1f, 1f, 0.85f, 1f);

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

    private Color _slotColor;
    [Tooltip("Sprite to use when the slot is empty (background frame).")]
    [SerializeField] private Sprite emptySlotSprite;

    // New: selection state for selling
    public int SelectedToSell { get; private set; } = 0;
    public bool IsSelected { get; private set; } = false;

    public event Action<InventorySlot> OnSlotSelected;
    public event Action<InventorySlot> OnSelectionChanged;

    private void Awake()
    {
        var img = GetComponent<Image>();
        img.raycastTarget = true;

        if (canvasGroup == null) canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;

        dragCanvas = GameObject.FindGameObjectWithTag("PopupCanvas")?.GetComponent<Canvas>();
        if (icon != null) _slotColor = icon.color;

        mainCamera = GameObject.FindGameObjectWithTag("MainCamera")?.GetComponent<NewCameraControls>();

        // Wire global buttons (only once)
        WireGlobalButtonsOnce();
        RefreshGlobalButtonsInteractable();
    }

    private void OnDestroy()
    {
        if (_currentSelected == this) _currentSelected = null;
        // Keep global buttons wired; other slots still need them
    }

    // Wire serialized buttons into static ones once so all slots can refresh them
    private void WireGlobalButtonsOnce()
    {
        if (_buttonsWired) return;

        _PLUS = plusButton;
        _MINUS = minusButton;
        _MIN = minButton;
        _MAX = maxButton;
        _SELL = sellButton;

        _PLUS?.onClick.AddListener(() => { if (_currentSelected != null) _currentSelected.IncrementSelection(1); RefreshGlobalButtonsInteractable(); });
        _MINUS?.onClick.AddListener(() => { if (_currentSelected != null) _currentSelected.DecrementSelection(1); RefreshGlobalButtonsInteractable(); });
        _MIN?.onClick.AddListener(() => { if (_currentSelected != null) _currentSelected.SetSelectionMin(); RefreshGlobalButtonsInteractable(); });
        _MAX?.onClick.AddListener(() => { if (_currentSelected != null) _currentSelected.SetSelectionMax(); RefreshGlobalButtonsInteractable(); });
        _SELL?.onClick.AddListener(() => { if (_currentSelected != null) _currentSelected.ExecuteSellSelected(); RefreshGlobalButtonsInteractable(); });

        _buttonsWired = true;
    }

    private static void RefreshGlobalButtonsInteractable()
    {
        bool hasSelection = _currentSelected != null && !_currentSelected.IsEmpty;
        int selectedToSell = hasSelection ? _currentSelected.SelectedToSell : 0;
        int amount = hasSelection ? _currentSelected.Amount : 0;

        // Enable Plus when there is a selection and capacity to select more (amount > selected)
        if (_PLUS) _PLUS.interactable = hasSelection && selectedToSell < amount;
        // Enable Minus only when there is something selected to decrement
        if (_MINUS) _MINUS.interactable = hasSelection && selectedToSell > 0;
        // Min/Max enabled when a slot is selected (regardless of SelectedToSell)
        if (_MIN) _MIN.interactable = hasSelection;
        if (_MAX) _MAX.interactable = hasSelection;
        // Sell enabled only when there is a selected quantity to sell
        if (_SELL) _SELL.interactable = hasSelection && selectedToSell > 0;
    }

    public void PlaceItem(InventoryItem item)
    {
        if (item == null) return;
        SetItem(item.SlotItem, item.SlotQuantity);
        Destroy(item.gameObject);
    }

    // Directly assigns material + amount
    public void SetItem(MaterialData mat, int amount)
    {
        MaterialData prevItem = Item;
        int prevAmount = Amount;

        Item = mat;
        Amount = Mathf.Max(0, amount);
        if (Amount == 0) Item = null;

        // Reset selection when item changes
        ClearSelection();

        UpdateUI();

        if (InventoryService.Instance != null && !InventoryService.Instance.IsLoading)
        {
            var delta = new Dictionary<int, int>();
            if (prevItem != null) delta[prevItem.id] = delta.TryGetValue(prevItem.id, out var v1) ? v1 - prevAmount : -prevAmount;
            if (mat != null) delta[mat.id] = delta.TryGetValue(mat.id, out var v2) ? v2 + Amount : Amount;

            var clean = new Dictionary<int, int>();
            foreach (var kv in delta) if (kv.Value != 0) clean[kv.Key] = kv.Value;
            if (clean.Count > 0) InventoryService.Instance.TryApply(clean);
        }

        if (_currentSelected == this) RefreshGlobalButtonsInteractable();
    }

    public void AddAmount(int delta)
    {
        if (IsEmpty && delta <= 0) return;
        MaterialData prevItem = Item;

        Amount += delta;
        if (Amount <= 0)
        {
            Clear();
        }
        else
        {
            // Clamp selection to new available amount
            SelectedToSell = Mathf.Clamp(SelectedToSell, 0, Amount);
            UpdateUI();
            OnSelectionChanged?.Invoke(this);
            RefreshGlobalButtonsInteractable();
        }

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

        // If this cleared slot was selected, deselect and clear current
        if (_currentSelected == this)
        {
            _currentSelected = null;
        }

        ClearSelection();
        UpdateUI();
        RefreshGlobalButtonsInteractable();

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
                // Apply selection overlay color when selected
                icon.color = IsSelected ? selectedHighlight : Color.white;
            }
            else
            {
                icon.sprite = emptySlotSprite;
                icon.color = _slotColor;
            }
        }

        if (amountText != null) amountText.text = !IsEmpty ? Amount.ToString() : string.Empty;
        if (nameText != null) nameText.text = !IsEmpty ? GetDisplayName(Item) : string.Empty;
    }

    // ----- Selection API used by global buttons -----
    public void SelectSlot(bool selected)
    {
        IsSelected = selected && !IsEmpty;

        if (IsSelected)
        {
            // Make this the current slot and deselect previous
            if (_currentSelected != this)
            {
                var prev = _currentSelected;
                _currentSelected = this;

                if (prev != null && prev != this)
                {
                    prev.IsSelected = false;
                    prev.UpdateUI();
                    prev.OnSelectionChanged?.Invoke(prev);
                }
            }
        }
        else if (_currentSelected == this)
        {
            _currentSelected = null;
        }

        UpdateUI();
        OnSlotSelected?.Invoke(this);
        RefreshGlobalButtonsInteractable();
    }

    public void SetSelectionMin()
    {
        if (IsEmpty) return;
        SelectedToSell = 0;
        UpdateUI();
        OnSelectionChanged?.Invoke(this);
        RefreshGlobalButtonsInteractable();
    }

    public void SetSelectionMax()
    {
        if (IsEmpty) return;
        SelectedToSell = Amount;
        UpdateUI();
        OnSelectionChanged?.Invoke(this);
        RefreshGlobalButtonsInteractable();
    }

    public void IncrementSelection(int step = 1)
    {
        if (IsEmpty || step <= 0) return;
        SelectedToSell = Mathf.Clamp(SelectedToSell + step, 0, Amount);
        UpdateUI();
        OnSelectionChanged?.Invoke(this);
        RefreshGlobalButtonsInteractable();
    }

    public void DecrementSelection(int step = 1)
    {
        if (IsEmpty || step <= 0) return;
        SelectedToSell = Mathf.Clamp(SelectedToSell - step, 0, Amount);
        UpdateUI();
        OnSelectionChanged?.Invoke(this);
        RefreshGlobalButtonsInteractable();
    }

    public void ExecuteSellSelected()
    {
        if (IsEmpty || SelectedToSell <= 0) return;

        // Reduce amount by selected-to-sell
        AddAmount(-SelectedToSell);

        // After selling, reset selection without losing current slot focus
        SelectedToSell = 0;
        UpdateUI();
        OnSelectionChanged?.Invoke(this);
        RefreshGlobalButtonsInteractable();
    }

    private void ClearSelection()
    {
        IsSelected = false;
        SelectedToSell = 0;
        // Refresh here too so buttons reflect cleared selection for the current slot
        RefreshGlobalButtonsInteractable();
    }

    // ----- Drag & Drop -----
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty) return;

        mainCamera?.SetInputLocked(true);

        var parent = canvasGroup ? canvasGroup.transform : (GetComponentInParent<Canvas>()?.transform ?? transform.root);
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
            ExecuteEvents.Execute<IDragHandler>(_activeProxy, e, ExecuteEvents.dragHandler);
            return;
        }
    }

    private void OnDisable() { if (icon) icon.color = _slotColor; _activeProxy = null; }

    public void OnEndDrag(PointerEventData e)
    {
        if (_activeProxy != null)
        {
            mainCamera?.SetInputLocked(false);
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
            var origin = dragged.CurrentSlot;
            var prevItem = Item;
            var prevAmount = Amount;

            SetItem(dragged.SlotItem, dragged.SlotQuantity);

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
        if (IsEmpty) return;

        SelectSlot(true);
        TutorialEventBus.PublishInventoryItemSelected();
    }
}
