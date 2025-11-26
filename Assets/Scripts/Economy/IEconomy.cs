using UnityEngine;

public interface IEconomy
{
    int GetMachineCost(MachineData machineData);
    int GetMaterialCost(MaterialData materialData);
    bool PurchaseMachine(MachineData machineData, ref int playerBalance);
    bool PurchaseConveyor(ConveyorBelt conveyorData, ref int playerBalance);
    bool PurchaseMaterial(MaterialData materialData, int quantity, ref int playerBalance);

    bool ReturnMachine(MachineData machineData, ref int playerBalance); 
    bool ReturnConveyor(ConveyorBelt conveyorData, ref int playerBalance);

    bool SellMaterial(MaterialData materialData, int quantity, ref int playerBalance);
}
