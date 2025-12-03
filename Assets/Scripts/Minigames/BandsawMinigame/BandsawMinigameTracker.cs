using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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

    [Header("Standalone Mode (when NOT launched via RepairMinigameManager)")]
    [SerializeField] private float standaloneRequiredScore = 0.8f;
    [SerializeField] private string standaloneReturnScene = "Demo";
    [SerializeField] private bool loadReturnSceneOnComplete = true;
    [SerializeField] private bool loadReturnSceneOnExit = true;

    [Header("Optional Threshold Source")]
    [SerializeField] private ScoreThresholdEvaluator standaloneThresholdEvaluator;

    float _requiredScore = 1f;
    float _latestScore = 0f;
    bool _completed;

    void Awake()
    {
        if (completionPanel) completionPanel.SetActive(false);
    }

    void OnEnable()
    {
        if (!evaluator)
            evaluator = FindObjectOfType<PatternCutEvaluator>();

        if (evaluator)
            evaluator.OnScoreComputed += OnScoreComputed;
        else
            Debug.LogWarning("BandsawMinigameTracker: PatternCutEvaluator reference missing. Evaluation will not work.");

        SetupThreshold();
        WireUI();
        UpdateScoreLabel();
    }

    void OnDisable()
    {
        if (evaluator) evaluator.OnScoreComputed -= OnScoreComputed;
    }

    private void SetupThreshold()
    {
        // Prefer repair session thresholds whenever a session is active
        if (RepairMinigameManager.HasActive)
        {
            _requiredScore = 1f;
            var def = RepairMinigameManager.CurrentMinigame;
            if (def != null && def.evaluator is ScoreThresholdEvaluator st)
                _requiredScore = Mathf.Clamp01(st.requiredNormalizedScore);
            else
                _requiredScore = 1f;
        }
        else
        {
            // Standalone thresholds
            if (standaloneThresholdEvaluator != null)
                _requiredScore = Mathf.Clamp01(standaloneThresholdEvaluator.requiredNormalizedScore);
            else
                _requiredScore = Mathf.Clamp01(standaloneRequiredScore);
        }

        if (thresholdText)
            thresholdText.text = $"Target: {(_requiredScore * 100f).ToString($"F{decimals}")}{percentSuffix}";
    }

    private void WireUI()
    {
        if (evaluateButton)
        {
            evaluateButton.onClick.RemoveAllListeners();
            evaluateButton.onClick.AddListener(() =>
            {
                if (evaluator) evaluator.EvaluateCut();
                else Debug.LogWarning("BandsawMinigameTracker: Evaluate clicked but no evaluator assigned.");
            });
        }

        if (completionReturnButton)
        {
            completionReturnButton.onClick.RemoveAllListeners();
            completionReturnButton.onClick.AddListener(() =>
            {
                if (!_completed) return;

                if (RepairMinigameManager.HasActive)
                {
                    // Repair flow
                    RepairMinigameManager.ReportResult(new MinigameResult
                    {
                        completed = true,
                        scoreNormalized = _latestScore
                    });
                }
                else
                {
                    // Standalone return
                    if (loadReturnSceneOnComplete && !string.IsNullOrEmpty(standaloneReturnScene))
                        LoadReturnSceneIfNeeded();
                }
            });
        }

        if (completionExitButton)
        {
            completionExitButton.onClick.RemoveAllListeners();
            completionExitButton.onClick.AddListener(() =>
            {
                if (RepairMinigameManager.HasActive)
                {
                    // Early exit fails repair
                    RepairMinigameManager.ExitWithoutRepair(_latestScore);
                }
                else
                {
                    // Standalone exit
                    if (loadReturnSceneOnExit && !string.IsNullOrEmpty(standaloneReturnScene))
                        LoadReturnSceneIfNeeded();
                }
            });
        }
    }

    private void LoadReturnSceneIfNeeded()
    {
        var scene = SceneManager.GetSceneByName(standaloneReturnScene);
        if (!scene.IsValid() || !scene.isLoaded)
            SceneManager.LoadScene(standaloneReturnScene, LoadSceneMode.Single);
        else
            SceneManager.SetActiveScene(scene);
    }

    private void OnScoreComputed(float score01)
    {
        _latestScore = Mathf.Clamp01(score01);
        UpdateScoreLabel();

        if (!_completed && _latestScore >= _requiredScore)
        {
            _completed = true;
            ShowCompletionPanel();
        }
    }

    private void UpdateScoreLabel()
    {
        if (latestScoreText)
            latestScoreText.text = $"Score: {(_latestScore * 100f).ToString($"F{decimals}")}{percentSuffix}";
    }

    private void ShowCompletionPanel()
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
