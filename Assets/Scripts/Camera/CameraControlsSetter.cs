using UnityEngine;
using UnityEngine.UI;
public class CameraControlsSetter : MonoBehaviour
{
    [SerializeField] private CameraController cameraController;
    [SerializeField] private CameraModeTestManager testManager;

    [Header("Optional: assign buttons or wire via OnClick in Inspector")]
    [SerializeField] private Button buttonA;
    [SerializeField] private Button buttonB;
    [SerializeField] private Button buttonC;
    [SerializeField] private Button buttonD;

    [SerializeField] private bool debugLogs = false;

    private void Awake()
    {
        if (buttonA != null) buttonA.onClick.AddListener(SetModeA);
        if (buttonB != null) buttonB.onClick.AddListener(SetModeB);
        if (buttonC != null) buttonC.onClick.AddListener(SetModeC);
        if (buttonD != null) buttonD.onClick.AddListener(SetModeD);
    }

    private CameraModeTestManager EnsureManager()
    {
        if (testManager == null)
        {
            testManager = FindFirstObjectByType<CameraModeTestManager>();
            if (testManager == null && debugLogs) Debug.LogWarning("[CameraControlsSetter] CameraModeTestManager not found. Falling back to direct camera mode set only.");
        }
        return testManager;
    }

    public void SetModeA() => Select(CameraController.CameraTestMode.A_SimultaneousZoomRotate);
    public void SetModeB() => Select(CameraController.CameraTestMode.B_ExclusiveZoomOrRotate);
    public void SetModeC() => Select(CameraController.CameraTestMode.C_TwoFingerSameDirectionRotate);
    public void SetModeD() => Select(CameraController.CameraTestMode.D_OneFingerRotate_TwoFingerPan);

    private void Select(CameraController.CameraTestMode mode)
    {
        var mgr = EnsureManager();
        if (mgr != null)
        {
            mgr.SelectMode(mode); // ensures currentMode is set and timer starts
        }
        else
        {
            (cameraController ??= FindFirstObjectByType<CameraController>())?.SetTestMode(mode);
        }
        if (debugLogs) Debug.Log($"[CameraControlsSetter] Requested mode: {mode}");
    }
}
