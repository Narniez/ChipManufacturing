using UnityEngine;

public abstract class MinigameEvaluator : ScriptableObject
{
    // Return true if machine should be repaired given result metrics.
    public abstract bool Evaluate(MinigameResult result);
}


