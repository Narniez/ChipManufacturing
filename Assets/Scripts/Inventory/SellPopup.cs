using System.Collections.Generic;
using System.Net.Mail;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SellPopup : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Slider amountSlider;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Canvas _popupCanvas;
    [SerializeField] private int LROfsset;
    [SerializeField] private int UDOffset;

    private InventoryItem _item;
    private InventorySlot _slot;

    private int _have;
    private Transform _originalParent;

    // Track open mode so we can avoid service/events in slot mode
    private bool _openedFromSlot;

    private NewCameraControls mainCamera;

    void Awake()
    {
        mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<NewCameraControls>();

        gameObject.SetActive(false);

        amountSlider.wholeNumbers = true;

        amountSlider.onValueChanged.AddListener(v =>
        {
            int val = Mathf.RoundToInt(v);
            if (amountInput.text != val.ToString())
                amountInput.text = val.ToString();
            UpdateFooter(val);
        });

        amountInput.onEndEdit.AddListener(t =>
        {
            if (!int.TryParse(t, out int val)) val = 1;
            val = Mathf.Clamp(val, 1, Mathf.Max(1, _have));
            if ((int)amountSlider.value != val) amountSlider.value = val;
            UpdateFooter(val);
        });

        confirmBtn.onClick.AddListener(OnConfirm);
    }

    // ---------------- OPEN / CLOSE ----------------

    public void OpenFor(InventoryItem item)
    {
        _openedFromSlot = false;

        _item = item;
        _slot = _item != null ? _item.CurrentSlot : null; // ensure repositioning works
        RefreshStockFromService();

        if (_item?.SlotItem != null && nameText)
            nameText.text = _item.SlotItem.materialName;

        amountSlider.minValue = 0;
        amountSlider.maxValue = Mathf.Max(1, _have);
        amountSlider.value = Mathf.Clamp(_have, (int)amountSlider.minValue, (int)amountSlider.maxValue);
        amountInput.text = ((int)amountSlider.value).ToString();

        UpdateFooter((int)amountSlider.value);
        confirmBtn.interactable = _have > 0;

        _originalParent = transform.parent;

        if (_popupCanvas == null)
            _popupCanvas = GameObject.FindGameObjectWithTag("PopupCanvas")?.GetComponent<Canvas>();

        if (_popupCanvas != null)
        {
            transform.SetParent(_popupCanvas.transform, worldPositionStays: false);
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            RepositionToItem();
        }
        else
        {
            Debug.LogWarning("SellPopup.OpenFor: no popup Canvas found. Popup may render under other UI.");
            gameObject.SetActive(true);
        }

        if (InventoryService.Instance != null)
            InventoryService.Instance.OnChanged += OnInventoryChanged;

        mainCamera.SetInputLocked(true);
    }

    // Open popup for a slot (no InventoryItem child required)
    public void OpenForSlot(InventorySlot slot)
    {
        _openedFromSlot = true;

        _slot = slot;
        _item = null;

        // Validate slot data
        if (_slot == null || _slot.IsEmpty || _slot.Item == null)
        {
            Close();
            return;
        }

        // In slot mode, treat the slot UI as the source of truth
        _have = Mathf.Max(0, _slot.Amount);

        if (nameText) nameText.text = _slot.Item.materialName;

        amountSlider.minValue = 0;
        amountSlider.maxValue = Mathf.Max(1, _have);
        amountSlider.value = Mathf.Clamp(_have, (int)amountSlider.minValue, (int)amountSlider.maxValue);
        amountInput.text = ((int)amountSlider.value).ToString();

        UpdateFooter((int)amountSlider.value);
        confirmBtn.interactable = _have > 0;

        _originalParent = transform.parent;

        if (_popupCanvas == null)
            _popupCanvas = GameObject.FindGameObjectWithTag("PopupCanvas")?.GetComponent<Canvas>();

        if (_popupCanvas != null)
        {
            transform.SetParent(_popupCanvas.transform, worldPositionStays: false);
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            RepositionToItem();
        }
        else
        {
            Debug.LogWarning("SellPopup.OpenFor: no popup Canvas found. Popup may render under other UI.");
            gameObject.SetActive(true);
        }

        mainCamera.SetInputLocked(true);

        // Important: do NOT subscribe to InventoryService events in slot mode,
        // as slot mode does not use the service as the authority.
    }

    public void Close()
    {
        if (InventoryService.Instance != null)
            InventoryService.Instance.OnChanged -= OnInventoryChanged;

        gameObject.SetActive(false);
        _item = null;
        _slot = null;

        if (_originalParent != null)
            transform.SetParent(_originalParent, worldPositionStays: false);

        mainCamera.SetInputLocked(false);
    }

    public void RepositionToItem()
    {
        if (_slot == null || _popupCanvas == null) return;

        var popupRT = transform as RectTransform;
        var itemRT = _slot.transform as RectTransform;
        if (popupRT == null || itemRT == null) return;

        Camera canvasCam = _popupCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _popupCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCam, itemRT.position);

        var popupCanvasRT = _popupCanvas.transform as RectTransform;
        if (popupCanvasRT == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(popupCanvasRT, screenPoint, canvasCam, out Vector2 localPoint);

        Vector2 offset = new Vector2(LROfsset, UDOffset);
        popupRT.anchoredPosition = localPoint + offset;
    }

    private void OnInventoryChanged(IDictionary<int, int> _, IReadOnlyDictionary<int, int> all)
    {
        // Ignore service changes when opened from slot
        if (_openedFromSlot) return;

        if (_item != null && _item.SlotItem != null)
        {
            int id = _item.SlotItem.id;
            int newHave = all != null && all.TryGetValue(id, out var c) ? c : 0;

            if (newHave == _have) return;
            _have = newHave;

            if (_have <= 0)
            {
                confirmBtn.interactable = false;
                Close();
                return;
            }

            amountSlider.maxValue = Mathf.Max(1, _have);
            if (amountSlider.value > _have) amountSlider.value = _have;
            amountInput.text = ((int)amountSlider.value).ToString();

            UpdateFooter((int)amountSlider.value);

            _item.RefreshFromService();
        }
        else
        {
            Close();
        }
    }

    private void RefreshStockFromService()
    {
        if (_openedFromSlot)
        {
            _have = (_slot != null && !_slot.IsEmpty) ? _slot.Amount : 0;
            _have = Mathf.Max(0, _have);
            return;
        }

        // Item mode: prefer authoritative counts from the service
        if (_item != null && _item.SlotItem != null)
        {
            if (InventoryService.Instance != null)
            {
                int serviceCount = InventoryService.Instance.GetCount(_item.SlotItem.id);
                _have = Mathf.Max(0, serviceCount);
            }
            else
            {
                _have = Mathf.Max(0, _item.SlotQuantity);
            }
        }
        else
        {
            _have = 0;
        }
    }

    /// ---------------- ACTIONS ----------------
    private void OnConfirm()
    {
        int amount = Mathf.RoundToInt(amountSlider.value);

        // Cache references to avoid race with callbacks that may Close() and null fields
        var slotRef = _slot;
        var itemRef = _item;
        var inventoryService = InventoryService.Instance;
        var economyManager = EconomyManager.Instance;

        // Slot path: use the slot as source-of-truth; do not call the service
        if (slotRef != null && !slotRef.IsEmpty && slotRef.Item != null)
        {
            // Clamp to available
            amount = Mathf.Clamp(amount, 1, slotRef.Amount);
            economyManager.playerBalance += amount * slotRef.Item.cost;      
            slotRef.AddAmount(-amount);      
            
            Close();
            return;
        }

        // Item path (service-authoritative)
        if (itemRef == null || itemRef.SlotItem == null) { Close(); return; }

        // Unsubscribe to avoid re-entrancy closing us mid-call
        if (inventoryService != null) inventoryService.OnChanged -= OnInventoryChanged;

        if (inventoryService != null)
            inventoryService.TryRemove(itemRef.SlotItem.id, amount);

        itemRef.RefreshFromService();
        Close();
    }

    private void UpdateFooter(int amount)
    {
        bool hasContext = (_item != null && _item.SlotItem != null) || (_slot != null && !_slot.IsEmpty && _slot.Item != null);
        confirmBtn.interactable = hasContext && amount >= 1 && amount <= _have;
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null)
            {
                Camera cam = _popupCanvas != null && _popupCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? _popupCanvas.worldCamera
                    : null;

                RectTransform popupRect = transform as RectTransform;

                if (popupRect != null && !RectTransformUtility.RectangleContainsScreenPoint(popupRect, Input.mousePosition, cam))
                {
                    Close();
                }
            }
        }
    }
}
