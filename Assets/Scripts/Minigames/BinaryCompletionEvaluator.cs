using UnityEngine;

[CreateAssetMenu(fileName = "BinaryCompletionEvaluator", menuName = "Scriptable Objects/Minigames/Minigame Evaluators/Binary")]
public class BinaryCompletionEvaluator : MinigameEvaluator
{
    public override bool Evaluate(MinigameResult result) => result.completed;
}