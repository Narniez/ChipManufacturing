using System;
using UnityEditor;
using UnityEngine;

public class TutorialEventBus : MonoBehaviour
{
    public static event Action OnShopOpened;
    public static event Action<MachineData> OnShopItemSelected;
    public static event Action<MachineData> OnShopBuyClicked;
    public static event Action<GameObject> OnPreviewStarted;
    public static event Action OnPreviewConfirmed;
    public static event Action<IGridOccupant> OnOccupantPlaced;
    public static event Action<IGridOccupant> OnSelectionChanged;
    public static event Action OnConveyorPreviewConfirmed;

    public static event Action OnInventoryOpened;
    public static event Action OnRecipeTreeOpened;
    public static event Action OnMachineFixClicked;
    public static event Action OnMachineBoostClicked;

    // STILL NEEDS DATA ATTACHED?
    public static event Action OnInventoryItemSelected;
    public static event Action OnInventoryItemSold;

    // Generic tutorial signal (single-subscribe option)
    public static event Action<TutorialSignal, object> OnSignalPublished;

    public static void PublishShopOpened()
    {
        OnShopOpened?.Invoke();
        PublishSignal(TutorialSignal.ShopOpened, null);
    }

    public static void PublishShopItemSelected(MachineData data)
    {
        OnShopItemSelected?.Invoke(data);
        PublishSignal(TutorialSignal.ShopItemSelected, data);
    }

    public static void PublishShopBuyClicked(MachineData data)
    {
        Debug.Log($"TutorialEventBus.PublishShopBuyClicked invoked (data={(data==null? "null": data.name)})");
        OnShopBuyClicked?.Invoke(data);
        PublishSignal(TutorialSignal.ShopBuyClicked, data);
    }

    public static void PublishPreviewStarted(GameObject prefab)
    {
        OnPreviewStarted?.Invoke(prefab);
        PublishSignal(TutorialSignal.PreviewStarted, prefab);
    }

    public static void PublishPreviewConfirmed()
    {
        OnPreviewConfirmed?.Invoke();
        PublishSignal(TutorialSignal.PreviewConfirmed, null);
    }

    public static void PublishConveyorPreviewConfirmed()
    {
        OnConveyorPreviewConfirmed?.Invoke();
        PublishSignal(TutorialSignal.ConveyorConnectedToMachine, null);
    }

    public static void PublishOccupantPlaced(IGridOccupant occ)
    {
        OnOccupantPlaced?.Invoke(occ);
        PublishSignal(TutorialSignal.MachinePlaced, occ);
    }

    public static void PublishSelectionChanged(IGridOccupant occ)
    {
        OnSelectionChanged?.Invoke(occ);
        // No dedicated TutorialSignal for selection change — send None so manager ignores it,
        // or extend TutorialSignal if you want steps to wait for selection changed.
        PublishSignal(TutorialSignal.None, occ);
    }

    public static void PublishInventoryOpened()
    {
        OnInventoryOpened?.Invoke();
        PublishSignal(TutorialSignal.InventoryOpened, null);
    }

    public static void PublishRecipeTreeOpened()
    {
        OnRecipeTreeOpened?.Invoke();
        PublishSignal(TutorialSignal.RecipeTreeOpened, null);
    }

    public static void PublishMachineFixButtonClicked()
    {
        OnMachineFixClicked?.Invoke();
        PublishSignal(TutorialSignal.ClickFixMachineButton, null);
    }

    public static void PublishMachineBoostButtonClicked()
    {
        OnMachineBoostClicked?.Invoke();
        PublishSignal(TutorialSignal.ClickBoostMachineButton, null);
    }

    // STILL NEEDS DATA ATTACHED?
    public static void PublishInventoryItemSelected()
    {
        OnInventoryItemSelected?.Invoke();
        PublishSignal(TutorialSignal.InventoryItemSelected, null);
    }

    public static void PublishInventoryItemSold()
    {
        OnInventoryItemSold?.Invoke();
        PublishSignal(TutorialSignal.InventoryItemSold, null);
    }

    public static void PublishSignal(TutorialSignal sig, object payload = null)
    {
        Debug.Log($"TutorialEventBus.PublishSignal: sig={sig}, payload={payload}");
        try
        {
            OnSignalPublished?.Invoke(sig, payload);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"TutorialEventBus.PublishSignal({sig}): subscriber threw: {ex}");
        }
    }
}
