using System.Collections.Generic;

public struct MinigameResult 
{
    // Set by the minigame

    // true if the player reached end condition
    public bool completed;

    // 0..1 (use 1 for binary success-only minigames)
    public float scoreNormalized;

    // optional metrics (timeLeft, accuracy, etc.)
    public Dictionary<string, float> extra; 
}
