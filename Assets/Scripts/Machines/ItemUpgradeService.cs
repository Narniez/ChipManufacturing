using UnityEngine;

public static class ItemUpgradeService
{
    /// <summary>
    /// Advances upgrade-cycle progress for <paramref name="item"/> based on the machine that just processed it.
    /// Returns true if the cycle was completed and the item should upgrade now.
    /// </summary>
    public static bool AdvanceCycle(ConveyorItem item, MachineData machine)
    {
        if (item == null || item.materialData == null || machine == null)
            return false;

        var mat = item.materialData;

        // No upgrade cycle configured -> nothing to do
        if (mat.requiredMachines == null || mat.requiredMachines.Count == 0)
            return false;

        int idx = Mathf.Clamp(item.upgradeCycleIndex, 0, mat.requiredMachines.Count);

        // Only complete the cycle when we match the expected machine in order.
        var expected = mat.requiredMachines[idx];

        if (expected == machine)
        {
            
            idx++;

           
            if (idx >= mat.requiredMachines.Count)
            {
                item.upgradeCycleIndex = 0;
                return true;
            }

            item.upgradeCycleIndex = idx;
            Debug.Log($"[ItemUpgradeService] Advanced upgrade cycle for item '{mat.materialName}' to step {item.upgradeCycleIndex}/{mat.requiredMachines.Count}");
            return false;
        }

        // Wrong machine:
        // Design choice: reset progress to 0 for strict ordering.

        item.upgradeCycleIndex = 0;

        if (mat.requiredMachines[0] == machine)
            item.upgradeCycleIndex = 1;

        return false;
    }

    public static bool TryUpgradeMaterial(ConveyorItem item)
    {
        if (item == null || item.materialData == null)
            return false;

        var current = item.materialData;

        if (current.upgradeMaterials == null || current.upgradeMaterials.Count == 0)
            return false;

        var next = current.upgradeMaterials[0];
        if (next == null || next == current)
            return false;

        item.materialData = next;
        return true;
    }
}