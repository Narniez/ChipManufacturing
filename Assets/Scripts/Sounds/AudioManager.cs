using System;
using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // Global clock event — UI button can call TriggerClockTick() to emit a tick.
    public static event Action OnClockTick;

    private Coroutine _clockCoroutine;

    // Called by a UI Button (hook in the Inspector)
    public void TriggerClockTick()
    {
        OnClockTick?.Invoke();
    }

    // Optional helpers to run the clock repeatedly (useful later)
    public void StartClockRepeating(float intervalSeconds)
    {
        StopClockRepeating();
        _clockCoroutine = StartCoroutine(ClockLoop(intervalSeconds));
    }

    public void StopClockRepeating()
    {
        if (_clockCoroutine != null)
        {
            try { StopCoroutine(_clockCoroutine); } catch { }
            _clockCoroutine = null;
        }
    }

    private IEnumerator ClockLoop(float interval)
    {
        while (true)
        {
            OnClockTick?.Invoke();
            yield return new WaitForSeconds(interval);
        }
    }

    private void OnDisable()
    {
        StopClockRepeating();
    }
}
