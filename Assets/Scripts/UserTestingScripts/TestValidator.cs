using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class TestValidator : MonoBehaviour
{
    [SerializeField] List<GameObject> letterObjects;
    [SerializeField] GameObject testCompleteUI;
    [SerializeField] TextMeshProUGUI timeText;

    [SerializeField] Color targetColor = Color.green;

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
            Debug.Log($"Time Passed: {timePassed:F2}s");
            if (testCompleteUI != null)
            {
                timeText.text = $"{timePassed:F2}s"; 
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
        startTimer = true;
    }

    static bool IsTargetColor(GameObject obj, Color target, float tolerance = 0.001f)
    {
        // TextMeshPro (UGUI or 3D)
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
