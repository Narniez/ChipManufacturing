using System.Collections.Generic;
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

    [Header("Pricing (placeholder)")]
    [SerializeField] private int pricePerUnit = 1;

    private InventoryItem _item;
    private InventorySlot _slot;

    private int _have;

    private Transform _originalParent;

    void Awake()
    {
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
        _item = item;
        RefreshStockFromService();

        if (_item.SlotItem != null && nameText)
            nameText.text = _item.SlotItem.materialName;

        amountSlider.minValue = 1;
        amountSlider.maxValue = Mathf.Max(1, _have);
        amountSlider.value = Mathf.Clamp(_have, (int)amountSlider.minValue, (int)amountSlider.maxValue);
        amountInput.text = ((int)amountSlider.value).ToString();

        UpdateFooter((int)amountSlider.value);
        confirmBtn.interactable = _have > 0;

        _originalParent = transform.parent;

        if (_popupCanvas == null)
            _popupCanvas = GameObject.FindGameObjectWithTag("PopupCanvas")?.GetComponent<Canvas>();

        var popupRT = transform as RectTransform;
        Vector3 worldPos = popupRT.position;

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
    }

    // New: open popup for a slot
    public void OpenForSlot(InventorySlot slot)
    {
        _slot = slot;
        _item = null;
        gameObject.SetActive(true);
        RefreshStockFromService();
      
        //nameText.text = _item.SlotItem.materialName;

        amountSlider.minValue = 1;
        amountSlider.maxValue = Mathf.Max(1, _have);
        amountSlider.value = Mathf.Clamp(_have, (int)amountSlider.minValue, (int)amountSlider.maxValue);
        amountInput.text = ((int)amountSlider.value).ToString();

        UpdateFooter((int)amountSlider.value);
        confirmBtn.interactable = _have > 0;

        _originalParent = transform.parent;

        if (_popupCanvas == null)
            _popupCanvas = GameObject.FindGameObjectWithTag("PopupCanvas")?.GetComponent<Canvas>();

        var popupRT = transform as RectTransform;
        Vector3 worldPos = popupRT.position;

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
    }

    public void Close()
    {
        gameObject.SetActive(false);
        _item = null;
        _slot = null;
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

        // Convert the item's position to canvas local point
        RectTransformUtility.ScreenPointToLocalPointInRectangle(popupCanvasRT, screenPoint, canvasCam, out Vector2 localPoint);

        Vector2 offset = new Vector2(LROfsset, UDOffset);
        popupRT.anchoredPosition = localPoint + offset;
    }

    private void OnInventoryChanged(IDictionary<int, int> _, IReadOnlyDictionary<int, int> all)
    {
        if (_item == null || _item.SlotItem == null) { Close(); return; }

        // Only react to the material shown in this popup
        int id = _item.SlotItem.id;
        int newHave = all != null && all.TryGetValue(id, out var c) ? c : 0;

        if (newHave == _have) return; // nothing changed

        _have = newHave;

        // If stock dropped to 0 while open, disable confirm and auto-close
        if (_have <= 0)
        {
            confirmBtn.interactable = false;
            Close();
            return;
        }

        // Clamp slider/input to new range
        amountSlider.maxValue = Mathf.Max(1, _have);
        if (amountSlider.value > _have) amountSlider.value = _have;
        amountInput.text = ((int)amountSlider.value).ToString();

        UpdateFooter((int)amountSlider.value);

        _item.RefreshFromService();
    }

    private void RefreshStockFromService()
    {
        if (_item?.SlotItem == null) { _have = 0; return; }

        if (InventoryService.Instance != null)
        {
            int serviceCount = InventoryService.Instance.GetCount(_item.SlotItem.id);
            if (serviceCount > 0) _have = serviceCount;
            _have = _item.SlotQuantity;
        }
        else _have = _item.SlotQuantity;

        _have = Mathf.Max(0, _have);
    }

    /// ---------------- ACTIONS ----------------
    private void OnConfirm()
    {
        if (_item == null || _item.SlotItem == null) { Close(); return; }
        int amount = Mathf.RoundToInt(amountSlider.value);
        //int amount = Mathf.RoundToInt(int.Parse(amountInput.text));

        // Sell through the service 
        if (InventoryService.Instance != null)
            InventoryService.Instance.TryRemove(_item.SlotItem.id, amount);

        // Refresh item UI from service after selling
        _item.RefreshFromService();

        Close();
    }

    private void UpdateFooter(int amount)
    {

        confirmBtn.interactable = _item != null && amount >= 1 && amount <= _have;
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
