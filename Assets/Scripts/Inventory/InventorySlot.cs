using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(UnityEngine.UI.Image))]
public class InventorySlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("Debug")]
    [SerializeField] private bool debug = true;

    [Header("UI")]
    [Tooltip("Icon image to display the item's sprite")]
    [SerializeField] private Image icon;
    [Tooltip("Text to display the stack count")]
    [SerializeField] private TextMeshProUGUI amountText;

    [SerializeField] private SellPopup sellPopup;



    // Backing data for this slot
    public MaterialData Item { get; private set; }
    public int Amount { get; private set; }
    public bool IsEmpty => Item == null || Amount <= 0;

    private Sprite _slotSprite;
    private TextMeshProUGUI _itemAmount;

    private void Awake()
    {
        var img = GetComponent<Image>();
        img.raycastTarget = true;

        //UpdateUi();
    }

    public void PlaceItem(InventoryItem item)
    {
        if (item == null) return;

        // Copy data from the InventoryItem, then destroy it.
        SetItem(item.SlotItem, item.SlotQuantity);

        // We no longer keep UI children
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
        if (Amount <= 0)
        {
            Clear();
        }
        else
        {
            UpdateUI();
        }
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
                icon.enabled = false;
                icon.sprite = null;
            }
        }

        if (amountText != null)
        {
            amountText.text = !IsEmpty ? Amount.ToString() : string.Empty;
        }
    }


    public void OnDrop(PointerEventData eventData)
    {
        // Accept drops of InventoryItem: copy its data into this slot and destroy it
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

        PlaceItem(dragged);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (sellPopup == null) return;

        if (sellPopup.gameObject.activeSelf)
            sellPopup.Close();
        else
            sellPopup.OpenForSlot(this);
    }
}
