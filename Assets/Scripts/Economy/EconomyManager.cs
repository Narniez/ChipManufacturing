using TMPro;
using UnityEngine;

public class EconomyManager : MonoBehaviour, IEconomy
{
    public static EconomyManager Instance { get; private set; }

    [SerializeField] private int defaultMaterialCost = 0;
    [SerializeField] public int defaultPlayerBalance = 500;

    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI playerBalanceText;

    [HideInInspector] public int playerBalance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playerBalance = defaultPlayerBalance;
        UpdateBalanceUI();
    }

    private void Update()
    {
        UpdateBalanceUI();
    }
    private void UpdateBalanceUI()
    {
        if (playerBalanceText != null)
        {
            playerBalanceText.text = $"{playerBalance}";
        }
    }

    public int GetMachineCost(MachineData machineData)
    {
        return machineData != null ? machineData.cost : 0;
    }

    public int GetMaterialCost(MaterialData materialData)
    {
        return materialData != null ? defaultMaterialCost : 0;
    }

    public bool PurchaseMachine(MachineData machineData, ref int playerBalance)
    {
        int cost = GetMachineCost(machineData);
        if (playerBalance >= cost)
        {
            playerBalance -= cost;
            return true;
        }
        return false;
    }

    public bool PurchaseMaterial(MaterialData materialData, int quantity, ref int playerBalance)
    {
        int cost = GetMaterialCost(materialData) * quantity;
        if (playerBalance >= cost)
        {
            playerBalance -= cost;
            return true;
        }
        return false;
    }

    public bool PurchaseConveyor(ConveyorBelt conveyorData, ref int playerBalance)
    {
        if (conveyorData == null) return false;
        if (playerBalance >= conveyorData.Cost)
        {
            playerBalance -= conveyorData.Cost;
            return true;
        }
        UpdateBalanceUI();

        return false;
    }

    public bool ReturnMachine(MachineData machineData, ref int playerBalance)
    {
        if (machineData == null)
            return false;

        // Refund full cost for now
        playerBalance += machineData.cost;

        // Update UI
        UpdateBalanceUI();

        return true;
    }

    public bool ReturnConveyor(ConveyorBelt conveyorData, ref int playerBalance)
    {
        if (conveyorData == null)
        {
            Debug.LogError("Conveyor data is null. Cannot return conveyor.");
            return false;
        }

        playerBalance += conveyorData.Cost;
        UpdateBalanceUI();
        return true;
    }

    public bool SellMaterial(MaterialData materialData, int quantity, ref int playerBalance)
    {
        throw new System.NotImplementedException();
    }
}
