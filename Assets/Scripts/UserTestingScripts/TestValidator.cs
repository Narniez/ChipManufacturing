using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class TestValidator : MonoBehaviour
{
    [SerializeField] List<GameObject> letterObjects;
    [SerializeField] GameObject testCompleteUI;
    [SerializeField] TextMeshProUGUI timeText;

    [SerializeField] Color targetColor = Color.green;
    [SerializeField] Color resetColor = Color.white; // used on reset (optional)

    public event System.Action<float> OnTestCompleted; // notify listeners
    public float LastTime { get; private set; } = 0f;

    float timePassed = 0f;
    bool startTimer = false;
    bool testPassed = false;
    bool testComplete = false;

    private void Update()
    {
        if (testComplete) return;

        if (startTimer)
        {
            TrackTime();
        }

        // Pass only if we have letters and ALL are the target color
        testPassed = letterObjects != null && letterObjects.Count > 0;
        if (testPassed)
        {
            foreach (GameObject obj in letterObjects)
            {
                if (obj == null || !IsTargetColor(obj, targetColor))
                {
                    testPassed = false;
                    break;
                }
            }
        }

        if (testPassed)
        {
            testComplete = true;
            startTimer = false;
            LastTime = timePassed;
            Debug.Log($"Time Passed: {timePassed:F2}s");

            // fire event before showing UI so other systems can react
            OnTestCompleted?.Invoke(LastTime);

            if (testCompleteUI != null)
            {
                if (timeText != null) timeText.text = $"{timePassed:F2}s";
                testCompleteUI.SetActive(true);
            }
        }
    }

    void TrackTime()
    {
        timePassed += Time.unscaledDeltaTime;
    }

    public void ActivateTimer()
    {
        // restart timer for a new run
        timePassed = 0f;
        startTimer = true;
        testPassed = false;
        testComplete = false;
        if (testCompleteUI != null) testCompleteUI.SetActive(false);
    }

    // Call from a "Continue" flow to hide UI and optionally reset letters for another run
    public void ResetAfterContinue(bool resetLetters = false)
    {
        if (testCompleteUI != null) testCompleteUI.SetActive(false);
        testComplete = false;
        startTimer = false;
        timePassed = 0f;

        if (resetLetters && letterObjects != null)
        {
            foreach (var obj in letterObjects)
            {
                if (obj == null) continue;
                var tmp = obj.GetComponent<TMP_Text>();
                if (tmp != null) tmp.color = resetColor;
            }
        }
    }

    static bool IsTargetColor(GameObject obj, Color target, float tolerance = 0.001f)
    {
        var tmp = obj.GetComponent<TMP_Text>();
        if (tmp != null) return Approximately(tmp.color, target, tolerance);
        return false;
    }

    static bool Approximately(Color a, Color b, float tol)
    {
        return Mathf.Abs(a.r - b.r) <= tol &&
               Mathf.Abs(a.g - b.g) <= tol &&
               Mathf.Abs(a.b - b.b) <= tol &&
               Mathf.Abs(a.a - b.a) <= tol;
    }
}
