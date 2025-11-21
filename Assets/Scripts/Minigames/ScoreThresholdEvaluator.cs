using UnityEngine;

[CreateAssetMenu(fileName = "ScoreThresholdEvaluator", menuName = "Scriptable Objects/Minigames/Minigame Evaluators/Score Threshold")]
public class ScoreThresholdEvaluator : MinigameEvaluator
{
    [Range(0f, 1f)] public float requiredNormalizedScore = 0.8f;
    public override bool Evaluate(MinigameResult result) =>
        result.completed && result.scoreNormalized >= requiredNormalizedScore;
}