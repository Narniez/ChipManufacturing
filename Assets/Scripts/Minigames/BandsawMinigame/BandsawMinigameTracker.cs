using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BandsawMinigameTracker : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PatternCutEvaluator evaluator;
    [SerializeField] private Button evaluateButton;
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private Button completionReturnButton;
    [SerializeField] private Button completionExitButton;
    [SerializeField] private TextMeshProUGUI thresholdText;
    [SerializeField] private TextMeshProUGUI latestScoreText;

    [Header("Formatting")]
    [Range(0, 3)] public int decimals = 1;
    public string percentSuffix = "%";

    float _requiredScore = 1f;
    float _latestScore = 0f;
    bool _completed;

    void Awake()
    {
        if (completionPanel) completionPanel.SetActive(false);
    }

    void OnEnable()
    {
        if (evaluator) evaluator.OnScoreComputed += OnScoreComputed;
        SetupThresholdFromEvaluator();
        WireUI();
        UpdateScoreLabel();
    }

    void OnDisable()
    {
        if (evaluator) evaluator.OnScoreComputed -= OnScoreComputed;
    }

    void SetupThresholdFromEvaluator()
    {
        _requiredScore = 1f; // default
        var def = RepairMinigameManager.CurrentMinigame;
        if (def != null && def.evaluator != null)
        {
            // If threshold-based evaluator, read its field
            if (def.evaluator is ScoreThresholdEvaluator st)
                _requiredScore = Mathf.Clamp01(st.requiredNormalizedScore);
            else
                _requiredScore = 1f; 
        }
        if (thresholdText)
            thresholdText.text = $"Target: {(_requiredScore * 100f).ToString($"F{decimals}")}{percentSuffix}";
    }

    void WireUI()
    {
        if (evaluateButton)
        {
            evaluateButton.onClick.RemoveAllListeners();
            evaluateButton.onClick.AddListener(() =>
            {
                if (evaluator) evaluator.EvaluateCut();
            });
        }
        if (completionReturnButton)
        {
            completionReturnButton.onClick.RemoveAllListeners();
            completionReturnButton.onClick.AddListener(() =>
            {
                if (!_completed)
                {
                    // Safety: ignore if not completed
                    return;
                }
                RepairMinigameManager.ReportResult(new MinigameResult
                {
                    completed = true,
                    scoreNormalized = _latestScore
                });
            });
        }
        if (completionExitButton)
        {
            completionExitButton.onClick.RemoveAllListeners();
            completionExitButton.onClick.AddListener(() =>
            {
                // Early exit fails repair
                RepairMinigameManager.ExitWithoutRepair(_latestScore);
            });
        }
    }

    void OnScoreComputed(float score01)
    {
        _latestScore = Mathf.Clamp01(score01);
        UpdateScoreLabel();

        if (!_completed && _latestScore >= _requiredScore)
        {
            _completed = true;
            ShowCompletionPanel();
        }
    }

    void UpdateScoreLabel()
    {
        if (latestScoreText)
            latestScoreText.text = $"Score: {(_latestScore * 100f).ToString($"F{decimals}")}{percentSuffix}";
    }

    void ShowCompletionPanel()
    {
        if (completionPanel)
            completionPanel.SetActive(true);
    }

    // Optional manual completion for binary evaluator (if you want a button instead of score)
    public void ForceComplete()
    {
        if (_completed) return;
        _completed = true;
        ShowCompletionPanel();
    }
}
