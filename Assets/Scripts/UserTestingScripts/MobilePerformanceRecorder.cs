using System.IO;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

public class MobilePerformanceRecorder : MonoBehaviour
{
    [Header("UI References")]
    public Button toggleButton;
    public TextMeshProUGUI buttonText; 
    public TextMeshProUGUI statusText;

    [Header("Settings")]
    public string fileName = "MobilePerformanceData.csv";

    // Profilers
    private ProfilerRecorder physicsTimeRecorder;
    private ProfilerRecorder totalMemoryRecorder;

    private bool isRecording = false;
    private float recordingTimer = 0f;
    private string fullFilePath;

    void OnEnable()
    {
        physicsTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate");
        totalMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
    }

    void OnDisable()
    {
        physicsTimeRecorder.Dispose();
        totalMemoryRecorder.Dispose();
    }

    void Start()
    {
        // Setup file path for MOBILE 
        fullFilePath = Path.Combine(Application.persistentDataPath, fileName);
        Debug.Log("Saving data to: " + fullFilePath);

        // Setup Button
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(OnToggleRecording);
        }

        // Initialize UI
        UpdateUIState();
    }

    public void OnToggleRecording()
    {
        isRecording = !isRecording;

        if (isRecording)
        {
            // If starting new recording, ensure header exists
            if (!File.Exists(fullFilePath))
            {
    
                File.WriteAllText(fullFilePath, "Time,FPS,FrameTime(ms),PhysicsTime(ms),TotalMemory(MB)\n");
            }
            recordingTimer = 0f; 
        }

        UpdateUIState();
    }

    void UpdateUIState()
    {
        if (buttonText != null)
            buttonText.text = isRecording ? "Stop Recording" : "Start Recording";

        if (statusText != null)
            statusText.text = isRecording ? $"Recording... ({fullFilePath})" : "Ready to Record";

        // Optional: Change button color
        if (toggleButton != null)
            toggleButton.image.color = isRecording ? Color.red : Color.green;
    }

    void Update()
    {
        if (!isRecording) return;

        recordingTimer += Time.deltaTime;

        // Record data every frame
        //LastValue is in Nanoseconds, multiply by 1e-6 to get Milliseconds
        double physicsTimeMS = physicsTimeRecorder.LastValue * (1e-6);
        float frameTimeMS = Time.deltaTime * 1000.0f;
        float memoryMB = totalMemoryRecorder.LastValue / (1024 * 1024);
        float fps = 1.0f / Time.deltaTime;

        // Create CSV Line
        string line = $"{recordingTimer:F2},{fps:F1},{frameTimeMS:F4},{physicsTimeMS:F4},{memoryMB:F2}\n";

        // Append to file
        File.AppendAllText(fullFilePath, line);

        // Update status text with live FPS if desired
        if (statusText != null)
            statusText.text = $"Rec: {recordingTimer:F1}s | FPS: {fps:F0} | Mem: {memoryMB:F0}MB";
    }
}
