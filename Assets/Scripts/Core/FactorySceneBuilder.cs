using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;

public class FactorySceneBuilder : MonoBehaviour
{
    [SerializeField] private PlacementManager placement;
    [SerializeField] private string factorySceneName = "Demo";

    private void Start()
    {
        StartCoroutine(RebuildFromSaveAsync());
    }

    private IEnumerator RebuildFromSaveAsync()
    {
        // Mark loading start so runtime systems (belt movement) pause while we rebuild.
        GameStateService.IsLoading = true;
        try
        {
            var scene = SceneManager.GetSceneByName(factorySceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("FactorySceneBuilder: factory scene not loaded yet.");
                yield break;
            }

            // Ensure PlacementManager reference. Wait for the PlacementManager singleton.
            int wait = 0;
            while (placement == null && PlacementManager.Instance == null && wait++ < 120)
                yield return null;
            if (placement == null) placement = PlacementManager.Instance;
            if (placement == null)
            {
                Debug.LogError("FactorySceneBuilder: PlacementManager not available. Aborting rebuild.");
                yield break;
            }

            // Wait for grid ready
            var grid = placement.GridService;
            wait = 0;
            while ((grid == null || !grid.HasGrid) && wait++ < 240)
            {
                yield return null;
                grid = placement.GridService;
            }
            if (grid == null || !grid.HasGrid)
            {
                Debug.LogError("FactorySceneBuilder: GridService not ready. Aborting rebuild.");
                yield break;
            }

            var gs = GameStateService.Instance?.State;
            if (gs == null) yield break;

            // Clean existing runtime placed machines/belts in the factory scene to avoid duplicates.
            // Only remove objects that belong to the factory scene (preserves DontDestroyOnLoad managers).
            var existingMachines = FindObjectsByType<Machine>(FindObjectsSortMode.None);
            foreach (var em in existingMachines)
            {
                if (em == null) continue;
                if (em.gameObject.scene == scene)
                {
                    // clear occupancy if registered
                    var size = em.BaseSize.OrientedSize(em.Orientation);
                    grid.SetAreaOccupant(em.Anchor, size, null);
                    Destroy(em.gameObject);
                }
            }
            var existingBelts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None);
            foreach (var eb in existingBelts)
            {
                if (eb == null) continue;
                if (eb.gameObject.scene == scene)
                {
                    grid.SetAreaOccupant(eb.Anchor, Vector2Int.one, null);
                    Destroy(eb.gameObject);
                }
            }

            var machinesSnapshot = new List<MachineState>(gs.machines);
            var beltsSnapshot = new List<BeltState>(gs.belts);

            // Machines first (use PlacementManager helper so all placement/occupancy logic stays centralized)
            foreach (var m in machinesSnapshot)
            {
                if (string.IsNullOrEmpty(m.machineDataPath))
                {
                    Debug.LogWarning($"Skipping machine at {m.anchor} due to empty machineDataPath. This means the type wasn't recorded when placed.");
                    continue;
                }

                // Try Addressables first, then Resources fallback
                MachineData data = null;
                var mh = Addressables.LoadAssetAsync<MachineData>(m.machineDataPath);
                yield return mh;
                if (mh.Status == AsyncOperationStatus.Succeeded)
                    data = mh.Result;
                else
                    data = Resources.Load<MachineData>(m.machineDataPath);

                if (data == null || data.prefab == null)
                {
                    Debug.LogWarning($"MachineData load failed for key '{m.machineDataPath}' at {m.anchor}. Skipping.");
                    continue;
                }

                // Use placement API so MoveToFactoryScene, occupancy and GameState sync behave the same as interactive placement.
                placement.PlaceMachineFromSave(data, m.anchor, m.orientation, m.isBroken);
                yield return null; // allow one frame so object registers with grid if needed
            }

            // Wait for MaterialVisualRegistry to be available before restoring visuals
            int mvWait = 0;
            while (MaterialVisualRegistry.Instance == null && mvWait++ < 120)
                yield return null;
            if (MaterialVisualRegistry.Instance == null)
                Debug.LogWarning("FactorySceneBuilder: MaterialVisualRegistry not available. Item visuals may not be restored.");

            // Then belts (use PlacementManager to keep behavior consistent)
            foreach (var b in beltsSnapshot)
            {
                var prefab = placement.GetConveyorPrefab(isTurn: b.isTurn);
                if (prefab == null)
                {
                    Debug.LogWarning($"FactorySceneBuilder: missing conveyor prefab for belt at {b.anchor}.");
                    continue;
                }

                // Place belt but DO NOT persist yet (we'll restore item then persist)
                var belt = placement.PlaceBeltFromSave(prefab, b.anchor, b.orientation, b.isTurn, b.turnKind, persist: false);
                if (belt == null) continue;

                if (BeltSystemRuntime.Instance != null)
                    BeltSystemRuntime.Instance.Register(belt);

                // Restore item on belt if present
                if (!string.IsNullOrEmpty(b.itemMaterialKey))
                {
                    MaterialData mat = null;
                    var ah = Addressables.LoadAssetAsync<MaterialData>(b.itemMaterialKey);
                    yield return ah;
                    if (ah.Status == AsyncOperationStatus.Succeeded)
                        mat = ah.Result;
                    else
                        mat = Resources.Load<MaterialData>(b.itemMaterialKey);

                    if (mat != null)
                    {
                        GameObject visualPrefab = MaterialVisualRegistry.Instance != null
                            ? MaterialVisualRegistry.Instance.GetPrefab(mat.materialType)
                            : null;
                        GameObject visual = visualPrefab != null ? Instantiate(visualPrefab) : null;
                        var item = new ConveyorItem(mat, visual);

                        // Force-restore item during load 
                        belt.RestoreItem(item);

                        // If restore failed for some reason and we created a visual, clean up
                        if (!belt.HasItem && visual != null)
                            Destroy(visual);

                        // Now persist the belt state WITH the item info
                        GameStateSync.TryAddOrUpdateBelt(belt);
                    }
                    else
                    {
                        Debug.LogWarning($"FactorySceneBuilder: failed to restore belt item for key '{b.itemMaterialKey}' at {b.anchor}");
                    }
                }
                yield return null; // allow frame for occupancy registration
            }

            // Final pass: refresh links once occupancy and objects are in place
            var allBelts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None);
            foreach (var belt in allBelts)
            {
                belt.RefreshChainLinks();
                belt.NotifyAdjacentMachinesOfConnection();
            }

            // Validation: report any anchors that are still unoccupied (helps debug)
            foreach (var ms in gs.machines)
            {
                if (!grid.TryGetCell(ms.anchor, out var cell) || cell.occupant == null)
                    Debug.LogWarning($"Machine at {ms.anchor} has no occupant in GridService after load.");
            }
            foreach (var bs in gs.belts)
            {
                if (!grid.TryGetCell(bs.anchor, out var cell) || cell.occupant == null)
                    Debug.LogWarning($"Belt at {bs.anchor} has no occupant in GridService after load.");
            }
        }
        finally
        {
            // Allow runtime systems to resume
            GameStateService.IsLoading = false;
        }
    }
}