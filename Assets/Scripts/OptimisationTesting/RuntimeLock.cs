using UnityEngine;

public class RuntimeLock : MonoBehaviour
{
    [SerializeField] int targetFps = 60;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFps;
        Debug.Log($"[RuntimeLock] vSync={QualitySettings.vSyncCount}, targetFPS={Application.targetFrameRate}");
    }
}
