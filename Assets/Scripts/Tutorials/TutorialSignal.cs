using UnityEngine;

public enum TutorialSignal
{
    None = 0,
    ShopOpened,
    ShopItemSelected,
    ShopBuyClicked,
    PreviewStarted,
    PreviewConfirmed,
    MachinePlaced,
    
    ConveyorConnectedToMachine,
    MachineProducedMaterial,
    InventoryOpened,
    InventoryItemSelected,
    InventoryItemSold,
    RecipeTreeOpened,
    ClickFixMachineButton,
    ClickBoostMachineButton,
    ConveyorChainLengthReached
}
