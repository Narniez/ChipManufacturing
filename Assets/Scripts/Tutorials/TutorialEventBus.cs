using System;
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

    public static void PublishShopOpened() => OnShopOpened?.Invoke();
    public static void PublishShopItemSelected(MachineData data) => OnShopItemSelected?.Invoke(data);
    public static void PublishShopBuyClicked(MachineData data) => OnShopBuyClicked?.Invoke(data);
    public static void PublishPreviewStarted(GameObject prefab) => OnPreviewStarted?.Invoke(prefab);
    public static void PublishPreviewConfirmed() => OnPreviewConfirmed?.Invoke();
    public static void PublishOccupantPlaced(IGridOccupant occ) => OnOccupantPlaced?.Invoke(occ);
    public static void PublishSelectionChanged(IGridOccupant occ) => OnSelectionChanged?.Invoke(occ);
}
