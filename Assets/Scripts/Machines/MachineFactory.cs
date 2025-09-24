using UnityEngine;

public class MachineFactory : MonoBehaviour
{
    public Machine CreateMachine(MachineData data, Vector3 position)
    {
        GameObject go = Instantiate(data.prefab, position, Quaternion.identity);
        Machine machine = go.GetComponent<Machine>();

        machine.Initialize(data);
        return machine;
    }
}
