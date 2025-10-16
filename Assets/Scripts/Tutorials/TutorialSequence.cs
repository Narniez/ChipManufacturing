using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialSequence", menuName = "Scriptable Objects/Tutorial/Sequence")]
public class TutorialSequence : ScriptableObject
{
    public List<TutorialStep> steps = new List<TutorialStep>();
}
