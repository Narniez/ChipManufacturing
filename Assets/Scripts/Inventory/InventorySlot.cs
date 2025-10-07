using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Image))]
public class InventorySlot : MonoBehaviour, IDropHandler
{
    [Tooltip("Where the InventoryItem should live. Defaults to this.transform if null.")]
    public RectTransform content;

    private void Awake()
    {
        if (content == null) content = (RectTransform)transform;
        var img = GetComponent<UnityEngine.UI.Image>();
        img.raycastTarget = true;
    }

    public InventoryItem CurrentItem =>
        content.childCount > 0 ? content.GetChild(0).GetComponent<InventoryItem>() : null;

    public void PlaceItem(InventoryItem item)
    {
        if (item == null) return;
        item.transform.SetParent(content, false);
        var rt = (RectTransform)item.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        item.SetCurrentSlot(this);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<InventoryItem>() : null;
        if (dragged == null) return;

        var sourceSlot = dragged.CurrentSlot;
        if (sourceSlot == this) { dragged.NotifyDroppedHandled(); return; }

        var here = CurrentItem;

        // If this slot already has an item, send it back to the source slot (swap).
        if (here != null && sourceSlot != null)
            sourceSlot.PlaceItem(here);

        // Place the dragged item here.
        PlaceItem(dragged);

        // Tell the item the drop was handled so it doesn't snap back on EndDrag.
        dragged.NotifyDroppedHandled();
    }
}
