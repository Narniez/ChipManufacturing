using UnityEngine;
using UnityEngine.SceneManagement;

public class FactorySceneBuilder : MonoBehaviour
{
    [SerializeField] private MachineFactory machineFactory;
    [SerializeField] private PlacementManager placement;
    [SerializeField] private string factorySceneName = "Demo";

    private Scene _factoryScene;

    void Start()
    {
        _factoryScene = SceneManager.GetSceneByName(factorySceneName);
        if (!_factoryScene.IsValid() || !_factoryScene.isLoaded)
        {
            Debug.LogWarning("FactorySceneBuilder: factory scene not loaded yet.");
            return;
        }

        var gs = GameStateService.Instance?.State;
        if (gs == null) return;

        foreach (var m in gs.machines)
        {
            var data = LoadMachineData(m.machineDataPath);
            if (data == null) continue;

            var go = Instantiate(data.prefab);
            SceneManager.MoveGameObjectToScene(go, _factoryScene);

            var machine = go.GetComponent<Machine>();
            if (machine != null)
            {
                machine.Initialize(data);
                machine.SetPlacement(m.anchor, m.orientation);
                if (m.isBroken) machine.Break();
            }
        }

        foreach (var b in gs.belts)
        {
            var prefab = placement.GetConveyorPrefab(isTurn: b.isTurn);
            var go = Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(go, _factoryScene);

            var belt = go.GetComponent<ConveyorBelt>();
            if (belt != null)
            {
                belt.SetTurnKind((ConveyorBelt.BeltTurnKind)b.turnKind);
                belt.SetPlacement(b.anchor, b.orientation);
            }
        }
    }

    private MachineData LoadMachineData(string path)
    {
        return !string.IsNullOrEmpty(path) ? Resources.Load<MachineData>(path) : null;
    }
}