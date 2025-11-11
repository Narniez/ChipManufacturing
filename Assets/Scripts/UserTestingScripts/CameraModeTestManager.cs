using TMPro;
using UnityEngine;

public class CameraModeTestManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private NewCameraControls cameraController;
    [SerializeField] private TestValidator validator;

    [Header("UI")]
    [SerializeField] private GameObject selectionPanel; // panel with the 4 buttons

    [Header("Best Time Labels")]
    [SerializeField] private TextMeshProUGUI bestA;
    [SerializeField] private TextMeshProUGUI bestB;
    [SerializeField] private TextMeshProUGUI bestC;
    [SerializeField] private TextMeshProUGUI bestD;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private NewCameraControls.CameraTestMode currentMode = NewCameraControls.CameraTestMode.A_SimultaneousZoomRotate;
    private float pendingTime = -1f;

    private void Awake()
    {
        if (validator != null)
            validator.OnTestCompleted += HandleTestCompleted;
        else if (debugLogs)
            Debug.LogWarning("[CameraModeTestManager] Validator reference is null.");

        // Clean invalid stored values (0 or negative)
        SanitizeStoredBests();
        RefreshAllBestLabels();
    }

    private void OnDestroy()
    {
        if (validator != null)
            validator.OnTestCompleted -= HandleTestCompleted;
    }

    public void SelectMode(NewCameraControls.CameraTestMode mode)
    {
        // Clear any stale completion state
        pendingTime = -1f;
        validator?.ResetAfterContinue(resetLetters: true);

        currentMode = mode;
        cameraController?.SetTestMode(mode);

        // Reset camera to scene start pose
        //cameraController?.ResetToStart(immediate: true);

        if (debugLogs) Debug.Log($"[CameraModeTestManager] SelectMode -> {mode}");

        // Hide selection and start the run
        selectionPanel?.SetActive(false);
        validator?.ActivateTimer();
    }

    private void HandleTestCompleted(float time)
    {
        pendingTime = time;
        if (debugLogs) Debug.Log($"[CameraModeTestManager] Test completed. Pending time: {pendingTime:0.00}s for mode {currentMode}");
    }

    // Hook this to the Continue button in the "test complete" UI
    public void OnContinue()
    {
        if (pendingTime >= 0f)
        {
            TrySetBest(currentMode, pendingTime);
            pendingTime = -1f;
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[CameraModeTestManager] Continue pressed without a pending time.");
        }

        // Reset and return to selection
        validator?.ResetAfterContinue(resetLetters: true);
        selectionPanel?.SetActive(true);

        // Refresh labels to reflect latest saved values
        RefreshAllBestLabels();
    }

    // Hook this to the Exit button
    public void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void TrySetBest(NewCameraControls.CameraTestMode mode, float time)
    {
        // Ignore non-sensical times
        if (time <= 0f)
        {
            if (debugLogs) Debug.LogWarning($"[CameraModeTestManager] Ignoring non-positive time: {time:0.00}s");
            return;
        }

        string key = KeyForMode(mode);
        bool hasBest = PlayerPrefs.HasKey(key);
        float prev = hasBest ? PlayerPrefs.GetFloat(key) : float.PositiveInfinity;

        // Treat 0 or negative as invalid/stale and overwrite
        bool prevInvalid = !hasBest || prev <= 0f;
        if (prevInvalid || time < prev)
        {
            PlayerPrefs.SetFloat(key, time);
            PlayerPrefs.Save();
            if (debugLogs) Debug.Log($"[CameraModeTestManager] Saved best for {mode}: {time:0.00}s (prev: {(prevInvalid ? "none/invalid" : prev.ToString("0.00"))})");
        }
        else if (debugLogs)
        {
            Debug.Log($"[CameraModeTestManager] Kept best for {mode}: {prev:0.00}s (new: {time:0.00}s)");
        }

        UpdateBestLabel(mode);
    }

    private void SanitizeStoredBests()
    {
        Sanitize(NewCameraControls.CameraTestMode.A_SimultaneousZoomRotate);
        Sanitize(NewCameraControls.CameraTestMode.B_ExclusiveZoomOrRotate);
        Sanitize(NewCameraControls.CameraTestMode.C_TwoFingerSameDirectionRotate);
        Sanitize(NewCameraControls.CameraTestMode.D_OneFingerRotate_TwoFingerPan);
        PlayerPrefs.Save();
    }

    private void Sanitize(NewCameraControls.CameraTestMode mode)
    {
        string key = KeyForMode(mode);
        if (PlayerPrefs.HasKey(key))
        {
            float v = PlayerPrefs.GetFloat(key);
            if (v <= 0f)
            {
                PlayerPrefs.DeleteKey(key);
                if (debugLogs) Debug.Log($"[CameraModeTestManager] Removed invalid best for {mode} (value: {v})");
            }
        }
    }

    private void RefreshAllBestLabels()
    {
        UpdateBestLabel(NewCameraControls.CameraTestMode.A_SimultaneousZoomRotate);
        UpdateBestLabel(NewCameraControls.CameraTestMode.B_ExclusiveZoomOrRotate);
        UpdateBestLabel(NewCameraControls.CameraTestMode.C_TwoFingerSameDirectionRotate);
        UpdateBestLabel(NewCameraControls.CameraTestMode.D_OneFingerRotate_TwoFingerPan);
    }

    private void UpdateBestLabel(NewCameraControls.CameraTestMode mode)
    {
        string key = KeyForMode(mode);
        if (PlayerPrefs.HasKey(key))
        {
            float t = PlayerPrefs.GetFloat(key);
            SetLabelForMode(mode, "Best time: " + FormatTime(t));
            if (debugLogs) Debug.Log($"[CameraModeTestManager] Label {mode} -> {t:0.00}s");
        }
        else
        {
            SetLabelForMode(mode, "Best time: ");
            if (debugLogs) Debug.Log($"[CameraModeTestManager] Label {mode} -> no best");
        }
    }

    private void SetLabelForMode(NewCameraControls.CameraTestMode mode, string text)
    {
        switch (mode)
        {
            case NewCameraControls.CameraTestMode.A_SimultaneousZoomRotate:
                if (bestA != null) bestA.text = text; else if (debugLogs) Debug.LogWarning("[CameraModeTestManager] bestA is not assigned.");
                break;
            case NewCameraControls.CameraTestMode.B_ExclusiveZoomOrRotate:
                if (bestB != null) bestB.text = text; else if (debugLogs) Debug.LogWarning("[CameraModeTestManager] bestB is not assigned.");
                break;
            case NewCameraControls.CameraTestMode.C_TwoFingerSameDirectionRotate:
                if (bestC != null) bestC.text = text; else if (debugLogs) Debug.LogWarning("[CameraModeTestManager] bestC is not assigned.");
                break;
            case NewCameraControls.CameraTestMode.D_OneFingerRotate_TwoFingerPan:
                if (bestD != null) bestD.text = text; else if (debugLogs) Debug.LogWarning("[CameraModeTestManager] bestD is not assigned.");
                break;
        }
    }

    private static string KeyForMode(NewCameraControls.CameraTestMode mode)
    {
        switch (mode)
        {
            case NewCameraControls.CameraTestMode.A_SimultaneousZoomRotate: return "BestTime_Mode_A";
            case NewCameraControls.CameraTestMode.B_ExclusiveZoomOrRotate: return "BestTime_Mode_B";
            case NewCameraControls.CameraTestMode.C_TwoFingerSameDirectionRotate: return "BestTime_Mode_C";
            case NewCameraControls.CameraTestMode.D_OneFingerRotate_TwoFingerPan: return "BestTime_Mode_D";
        }
        return "BestTime_Unknown";
    }

    private static string FormatTime(float seconds)
    {
        return $"{seconds:0.00}s";
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}
