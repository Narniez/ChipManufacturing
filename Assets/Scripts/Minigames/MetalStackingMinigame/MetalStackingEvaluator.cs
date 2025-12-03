using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MetalStackingEvaluator : MonoBehaviour
{
    [Header("Goal")]
    [SerializeField] private int targetLayers = 10;

    [Header("Completion")]
    [SerializeField] private UnityEvent onPuzzleComplete;
    [SerializeField] private GameObject quitButton;
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private Button completionReturnButton;
    [SerializeField] private Button completionExitButton;

    private UnityEvent onFail;
    private UnityEvent<int> onLayerCountChanged;

    private bool _finished;

    // Public read-only flag for external gating
    public bool IsFinished => _finished;

    private void OnEnable()
    {
        WireUI();
    }

    private void WireUI()
    {
        // Persistent early-exit (keeps machine broken)
        if (quitButton != null)
        {
            var btn = quitButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    RepairMinigameManager.ExitWithoutRepair(0f);
                });
            }
        }

        // Completion panel buttons (shown only after success)
        if (completionReturnButton != null)
        {
            completionReturnButton.onClick.RemoveAllListeners();
            completionReturnButton.onClick.AddListener(() =>
            {
                if (!_finished) return;
                RepairMinigameManager.ReportResult(new MinigameResult
                {
                    completed = true,
                    scoreNormalized = 1f
                });
            });
        }

        if (completionExitButton != null)
        {
            completionExitButton.onClick.RemoveAllListeners();
            completionExitButton.onClick.AddListener(() =>
            {
                // Exit back without repairing, even after completion if chosen
                RepairMinigameManager.ExitWithoutRepair(0f);
            });
        }
    }

    public void OnLayerCompleted(int totalLayers)
    {
        if (_finished)
        {
            return;
        }
        if (totalLayers >= targetLayers)
        {
            _finished = true;
            onPuzzleComplete?.Invoke();
            Debug.Log($"MetalStacking: Win! Layers={totalLayers}/{targetLayers}");
        }
    }

    public void Fail()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        onFail?.Invoke();
        Debug.Log("MetalStacking: Fail.");
        ResetRun();
    }

    public void ResetRun()
    {
        _finished = false;
        onLayerCountChanged?.Invoke(0);
    }

    public void SetTargetLayers(int layers)
    {
        targetLayers = Mathf.Max(1, layers);
    }
}
