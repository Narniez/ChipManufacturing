using UnityEngine;

public class Machine : MonoBehaviour
{
    [SerializeField] private MachineData data;

    public MachineData Data => data;

    private float timer;
    [SerializeField] private bool isActive = true;

    public void Initialize(MachineData machineData)
    {
        data = machineData;
        name = machineData.machineName;
    }

    private void Update()
    {
        if (!isActive) return;

        timer += Time.deltaTime;
        if (timer >= data.processingTime)
        {
            ProduceOutput();
            timer = 0;
        }
    }

    private void ProduceOutput()
    {
    
        
    }

    public void ToggleActive(bool active)
    {
        isActive = active;
    }
}
