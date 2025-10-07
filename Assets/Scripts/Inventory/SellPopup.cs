using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEditor;

public class SellPopup : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    //[SerializeField] private TMP_Text haveText;
    [SerializeField] private Slider amountSlider;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private Button confirmBtn;
    //[SerializeField] private Button cancelBtn;
    //[SerializeField] private TMP_Text totalValueText; 

    [Header("Pricing (placeholder)")]
    [SerializeField] private int pricePerUnit = 1;

    private InventoryItem _item; 
    private int _have;          

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
        //cancelBtn.onClick.AddListener(Close);
    }

    // ---------------- OPEN / CLOSE ----------------

    public void OpenFor(InventoryItem item)
    {
        _item = item;
        RefreshStockFromService();   // sets _have and clamps controls

        if (_item.SlotItem != null)
        {
            //if (icon) { icon.sprite = _item.SlotItem.icon; icon.enabled = icon.sprite != null; }
            if (nameText) nameText.text = _item.SlotItem.materialName;
        }

        amountSlider.minValue = 1;
        amountSlider.maxValue = Mathf.Max(1, _have);

        amountSlider.value = Mathf.Clamp(_have, (int)amountSlider.minValue, (int)amountSlider.maxValue);
        amountInput.text = ((int)amountSlider.value).ToString();

        UpdateFooter((int)amountSlider.value);
        confirmBtn.interactable = _have > 0;

        gameObject.SetActive(true);

        // subscribe for live updates
        if (InventoryService.Instance != null)
            InventoryService.Instance.OnChanged += OnInventoryChanged;
    }

    public void Close()
    {
        if (InventoryService.Instance != null)
            InventoryService.Instance.OnChanged -= OnInventoryChanged;

        gameObject.SetActive(false);
        _item = null;
    }


    private void OnInventoryChanged(IDictionary<int, int> _, IReadOnlyDictionary<int, int> all)
    {
        if (_item == null || _item.SlotItem == null) { Close(); return; }

        // Only react to the material shown in this popup
        int id = _item.SlotItem.id;
        int newHave = all != null && all.TryGetValue(id, out var c) ? c : 0;

        if (newHave == _have) return; // nothing changed

        _have = newHave;

        // If stock dropped to 0 while open, disable confirm and (optionally) auto-close
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
           if(serviceCount > 0) _have  = serviceCount;
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

        // Sell through the service (single source of truth)
        if (InventoryService.Instance != null)
            InventoryService.Instance.TryRemove(_item.SlotItem.id, amount);

        // Refresh item UI from service after selling
        _item.RefreshFromService();

        Close();
    }

    private void UpdateFooter(int amount)
    {
        /*if (totalValueText)
            totalValueText.text = $"Sell for: {amount * Mathf.Max(0, pricePerUnit)}";*/
        confirmBtn.interactable = _item != null && amount >= 1 && amount <= _have;
    }
}
