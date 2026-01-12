using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

/// DataLogger: data logger for repeatable performance runs
/// - Warmup (ignored)
/// - Measure window (frame time samples)
/// - Writes a single CSV summary row to Application.persistentDataPath/data.csv
/// Metrics:
/// - avg/p95/p99/max frame time (ms)
/// - GC collections (Gen0/1/2) during measure window
/// - Mono used memory delta (MB) during measure window
public class DataLogger : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float warmupSeconds = 10f;
    [SerializeField] private float measureSeconds = 60f;
    [SerializeField] private bool useUnscaledDeltaTime = true;

    [Header("Run Identification")]
    [SerializeField] private string testName = "Benchmark";
    [SerializeField] private string variant = "baseline";
    [SerializeField] private bool autoIncrementRunIndex = true;
    [SerializeField] private int runIndex = 1;

    [Header("Logging")]
    [SerializeField] private bool logPerSecondToConsole = true;

    // Frame-time samples (ms)
    private readonly List<float> _frameMs = new(8192);

    private enum Phase { Warmup, Measure, Done }
    private Phase _phase = Phase.Warmup;

    private float _t;
    private float _nextSecondMark;

    // GC + memory markers for measure window
    private int _gc0Start, _gc1Start, _gc2Start;
    private long _monoUsedStart;

    private void Start()
    {
        if (autoIncrementRunIndex)
        {
            string key = $"DL_RUN_{testName}_{variant}";
            int last = PlayerPrefs.GetInt(key, 0);
            runIndex = last + 1;
            PlayerPrefs.SetInt(key, runIndex);
            PlayerPrefs.Save();
        }

        _t = 0f;
        _nextSecondMark = 1f;
    }


    private void Update()
    {
        float dt = useUnscaledDeltaTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _t += dt;

        if (_phase == Phase.Warmup)
        {
            if (_t >= warmupSeconds)
            {
                BeginMeasure();
            }
            return;
        }

        if (_phase == Phase.Measure)
        {
            _frameMs.Add(dt * 1000f);

            if (logPerSecondToConsole && _t >= _nextSecondMark)
            {
                _nextSecondMark += 1f;
                float monoMB = Profiler.GetMonoUsedSizeLong() / 1048576f;
            }

            if (_t >= measureSeconds)
            {
                EndMeasureAndWrite();
            }
        }
    }

    private void BeginMeasure()
    {
        _phase = Phase.Measure;
        _t = 0f;
        _nextSecondMark = 1f;
        _frameMs.Clear();

        _gc0Start = GC.CollectionCount(0);
        _gc1Start = GC.CollectionCount(1);
        _gc2Start = GC.CollectionCount(2);
        _monoUsedStart = Profiler.GetMonoUsedSizeLong();

        Debug.Log($"[DataLogger] MEASURE_START test={testName} variant={variant} run={runIndex}");
    }

    private void EndMeasureAndWrite()
    {
        _phase = Phase.Done;

        if (_frameMs.Count == 0)
        {
            Debug.LogWarning("[DataLogger] No frame samples collected.");
            return;
        }

        _frameMs.Sort();

        float avg = 0f;
        for (int i = 0; i < _frameMs.Count; i++) avg += _frameMs[i];
        avg /= _frameMs.Count;

        float p95 = PercentileSorted(_frameMs, 0.95f);
        float p99 = PercentileSorted(_frameMs, 0.99f);
        float max = _frameMs[_frameMs.Count - 1];

        int gc0 = GC.CollectionCount(0) - _gc0Start;
        int gc1 = GC.CollectionCount(1) - _gc1Start;
        int gc2 = GC.CollectionCount(2) - _gc2Start;

        long monoUsedEnd = Profiler.GetMonoUsedSizeLong();
        float monoDeltaMB = (monoUsedEnd - _monoUsedStart) / 1048576f;

        WriteCsvRow(avg, p95, p99, max, gc0, gc1, gc2, monoDeltaMB);

        Debug.Log($"[DataLogger] MEASURE_END test={testName} variant={variant} run={runIndex}");
        Debug.Log($"[DataLogger] SUMMARY avg={avg:F2}ms p95={p95:F2}ms p99={p99:F2}ms max={max:F2}ms | GC(0/1/2)={gc0}/{gc1}/{gc2} | mono_delta={monoDeltaMB:F2}MB");

        Application.Quit();
    }

    private void WriteCsvRow(float avg, float p95, float p99, float max, int gc0, int gc1, int gc2, float monoDeltaMB)
    {
        string path = Path.Combine(Application.persistentDataPath, "data.csv");
        bool exists = File.Exists(path);

        using var sw = new StreamWriter(path, append: true);

        if (!exists)
        {
            sw.WriteLine("timestamp,test,variant,run,avg_ms,p95_ms,p99_ms,max_ms,gc0,gc1,gc2,mono_used_delta_mb,platform,unityVersion");
        }

        string ts = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
        sw.WriteLine($"{ts},{Escape(testName)},{Escape(variant)},{runIndex},{avg:F3},{p95:F3},{p99:F3},{max:F3},{gc0},{gc1},{gc2},{monoDeltaMB:F2},{Application.platform},{Escape(Application.unityVersion)}");
        sw.Flush();

        Debug.Log($"[DataLogger] WROTE CSV: {path}");
    }

    private static float PercentileSorted(List<float> sorted, float p)
    {
        if (sorted == null || sorted.Count == 0) return 0f;
        p = Mathf.Clamp01(p);

        float idx = (sorted.Count - 1) * p;
        int lo = Mathf.FloorToInt(idx);
        int hi = Mathf.CeilToInt(idx);
        if (lo == hi) return sorted[lo];

        float t = idx - lo;
        return Mathf.Lerp(sorted[lo], sorted[hi], t);
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // Basic CSV escaping
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
