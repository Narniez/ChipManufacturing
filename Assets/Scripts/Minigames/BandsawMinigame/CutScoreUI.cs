using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CutScoreUI : MonoBehaviour
{
    [Header("References")]
    public PatternCutEvaluator evaluator;   // assign your PatternCutEvaluator in Inspector
    public Button evaluateButton;           // optional; hook up if you want to enable/disable
    public TMP_Text scoreTextTMP;           // preferred (TextMeshProUGUI)            

    [Header("Formatting")]
    [Range(0, 3)] public int decimals = 1;
    public string prefix = "Score: ";
    public string suffix = "%";

    void OnEnable()
    {
        if (evaluator != null)
            evaluator.OnScoreComputed += OnScoreComputed;
    }

    void OnDisable()
    {
        if (evaluator != null)
            evaluator.OnScoreComputed -= OnScoreComputed;
    }

    // Hook this from your Button OnClick
    public void OnEvaluatePressed()
    {
        SetText("Evaluating...");
        if (evaluateButton) evaluateButton.interactable = false;
        evaluator?.EvaluateCut();
    }

    void OnScoreComputed(float score01)
    {
        // Convert 0..1 to percentage
        float percent = Mathf.Clamp01(score01) * 100f;
        string formatted = prefix + percent.ToString("F" + decimals) + suffix;

        SetText(formatted);
        if (evaluateButton) evaluateButton.interactable = true;
    }

    void SetText(string s)
    {
        if (scoreTextTMP) scoreTextTMP.text = s;
    }
}
