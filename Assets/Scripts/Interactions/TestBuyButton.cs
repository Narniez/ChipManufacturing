using UnityEngine;
using UnityEngine.UI;

public class TestBuyButton : MonoBehaviour
{
    [SerializeField] private MachineData machineData;
    [SerializeField] private bool disableWhilePlacing = true;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        var pm = PlacementManager.Instance;
        if (pm == null) return;


        // Ignore if already placing (prevents starting a new placement mid/after confirm)
        if (pm.IsPlacing) return;

        pm.StartPlacement(machineData);
    }
}
