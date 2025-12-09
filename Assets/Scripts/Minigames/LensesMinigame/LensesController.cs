using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LensesController : MonoBehaviour
{
    [Header("Lens Settings")]
    [Tooltip("Auto-find all Lens components in the scene at Start.")]
    [SerializeField] private bool autoFindLenses = true;
    [SerializeField] private List<GameObject> lenses = new();
    [SerializeField] private Transform emitter;
    [SerializeField] private LineRenderer line;

    [Header("Obstacle Settings")]
    [SerializeField] private GameObject obstaclePrefab;
    [Tooltip("Spawn this many obstacles when spawning is enabled.")]
    [SerializeField] private int obstacleCount = 5;

    [Header("Spawn Settings")]
    [Tooltip("Add slight random offset in X around each column base position.")]
    [SerializeField] private bool randomizeColumnX = true;
    [Tooltip("Prefab used to instantiate lenses at runtime.")]
    [SerializeField] private GameObject lensPrefab;
    [Tooltip("Spawn this many lenses when spawning is enabled.")]
    [SerializeField] private int spawnCount = 5;
    [Tooltip("X range (min, max) in world space where lenses will be placed.")]
    [SerializeField] private Vector2 spawnXRange = new(-5f, 5f);
    [Tooltip("Max horizontal offset (left/right) from the base column X position.")]
    [SerializeField, Range(0f, 10f)] private float columnXJitter = 0.5f;
    [Tooltip("Y range (min, max) in world space where lenses will be placed vertically.")]
    [SerializeField] private Vector2 spawnYRange = new(0.5f, 2.5f);
    [Tooltip("Minimum allowed vertical distance between spawned lenses in the same column.")]
    [SerializeField] private float minVerticalSpacing = 0.75f;
    [Tooltip("When true, existing serialized lenses in the list will be destroyed before spawning.")]
    [SerializeField] private bool clearExistingLensesOnSpawn = true;
    [Tooltip("Spawn lenses automatically during Start if a prefab is provided.")]
    [SerializeField] private bool spawnOnStart = true;
    [Tooltip("If true, spawn into two columns: one at spawnXRange.x and the other at spawnXRange.y.")]
    [SerializeField] private bool spawnInTwoColumns = true;

    [Header("Spawn Collision")]
    [Tooltip("Which layers should block lens/obstacle placement checks.")]
    [SerializeField] private LayerMask spawnBlockMask = ~0;
    [Tooltip("Minimum clearance radius to check around spawned obstacles.")]
    [SerializeField] private float obstaclePlacementClearance = 0.5f;
    [Tooltip("Minimum clearance radius to check around spawned lenses.")]
    [SerializeField] private float lensPlacementClearance = 0.5f;
    [Tooltip("How many attempts to try to find a free location per spawned object.")]
    [SerializeField] private int maxPlacementAttempts = 50;

    [Header("Bounce Settings")]
    [Tooltip("Total path length the beam can travel across all bounces.")]
    [SerializeField] private float maxDistance = 5000f;
    [Tooltip("Max number of reflections before stopping.")]
    [SerializeField] private int maxBounces = 8;
    [Tooltip("Layers the beam can hit.")]
    [SerializeField] private LayerMask rayMask = ~0;
    [Tooltip("Start direction. If false, uses the emitter's forward.")]
    [SerializeField] private bool startDownward = false;

    [Header("Completion")]
    public UnityEvent onPuzzleComplete;

    [SerializeField] private GameObject quitButton;

    // NEW: completion UI mirroring Bandsaw tracker
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private Button completionReturnButton;
    [SerializeField] private Button completionExitButton;

    private bool completed;
    private Lens currentSelected;
    private Vector3[] initialLinePositions;

    private const float SURFACE_OFFSET = 0.001f;
    private const float MIN_SEGMENT = 0.0005f; // prevent micro-bounce loops

    private List<GameObject> obstacles = new();

    private void Awake()
    {
        foreach (var lens in lenses)
        {
            if (lens != null)
                lens.SetActive(true);
        }

        if (line != null)
            line.useWorldSpace = true;

        if (completionPanel != null)
            completionPanel.SetActive(false);
    }

    private void OnEnable()
    {
        WireUI();
    }

    private void Start()
    {
       /* if (spawnOnStart && lensPrefab != null)
        {
            SpawnLenses();
            SpawnObstacles();
        }*/

        if (autoFindLenses)
            AutoFindLenses();
    }

    private void Update()
    {
        CastAndRenderBeam();
    }

    private void WireUI()
    {
        // Persistent early-exit (keeps machine broken)
        if (quitButton != null)
        {
            var btn = quitButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    RepairMinigameManager.ExitWithoutRepair(0f);
                });
            }
        }

        // Completion panel buttons (shown only after success)
        if (completionReturnButton != null)
        {
            completionReturnButton.onClick.RemoveAllListeners();
            completionReturnButton.onClick.AddListener(() =>
            {
                if (!completed) return;
                RepairMinigameManager.ReportResult(new MinigameResult
                {
                    completed = true,
                    scoreNormalized = 1f
                });
            });
        }

        if (completionExitButton != null)
        {
            completionExitButton.onClick.RemoveAllListeners();
            completionExitButton.onClick.AddListener(() =>
            {
                // Exit back without repairing, even after completion if chosen
                RepairMinigameManager.ExitWithoutRepair(0f);
            });
        }
    }

    public void RegisterLens(GameObject lens)
    {
        if (!lenses.Contains(lens))
            lenses.Add(lens);

        Debug.Log("Registered lens: " + lens.name);
    }

    private void AutoFindLenses()
    {
        lenses = FindObjectsOfType<Lens>().Select(l => l.gameObject).ToList();
    }

    #region Spawning

    private void SpawnObstacles()
    {
        if (obstaclePrefab == null) return;

        float z = emitter != null ? emitter.position.z : 0f;

        for (int i = 0; i < obstacleCount; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                float randomX = Random.Range(spawnXRange.x, spawnXRange.y);
                float randomY = Random.Range(spawnYRange.x - 5, spawnYRange.y - 5);
                var candidate = new Vector3(randomX, randomY, z);

                if (IsAreaFree(candidate, obstaclePlacementClearance))
                {
                    var obst = Instantiate(obstaclePrefab, candidate, Quaternion.identity, transform);
                    obstacles.Add(obst);

                    var rb = obst.GetComponent<Rigidbody>();
                    if (rb == null)
                        rb = obst.AddComponent<Rigidbody>();

                    rb.isKinematic = true;
                    rb.useGravity = false;

                    placed = true;
                    break;
                }
            }

            if (!placed)
                Debug.LogWarning($"LensesController: Failed to place obstacle {i} after {maxPlacementAttempts} attempts. Consider increasing spawn area or lowering clearance.");
        }
    }

  /*  private void SpawnLenses()
    {
        if (lensPrefab == null || spawnCount <= 0)
            return;

        if (clearExistingLensesOnSpawn)
            ClearExistingLenses();

        float z = emitter != null ? emitter.position.z : 0f;

        if (spawnInTwoColumns)
            SpawnTwoColumns(z);
    }*/

    private void ClearExistingLenses()
    {
        if (lenses == null || lenses.Count == 0) return;

        foreach (var g in lenses)
        {
            if (g == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(g);
            else
                Destroy(g);
#else
            Destroy(g);
#endif
        }

        lenses.Clear();
    }

    private void ClearObstacles()
    {
        if (obstacles == null || obstacles.Count == 0) return;

        foreach (var o in obstacles)
        {
            if (o == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(o);
            else
                Destroy(o);
#else
            Destroy(o);
#endif
        }

        obstacles.Clear();
    }
/*
    [ContextMenu("Regenerate Spawns")]
    public void RegenerateSpawns()
    {
        RegenerateSpawns(true, true);
    }*/

   /* public void RegenerateSpawns(bool clearLenses, bool clearObstacles)
    {
        if (clearObstacles)
            ClearObstacles();

        if (clearLenses)
            ClearExistingLenses();

        SpawnLenses();
        SpawnObstacles();

        if (autoFindLenses)
            AutoFindLenses();
    }*/

    private void SpawnTwoColumns(float z)
    {
        int leftCount = spawnCount / 2;
        int rightCount = spawnCount - leftCount;

        float leftX = spawnXRange.x;
        float rightX = spawnXRange.y;
        float minX = spawnXRange.x;
        float maxX = spawnXRange.y;

        var leftYs = GenerateRandomSpacedValues(leftCount, spawnYRange.x, spawnYRange.y, minVerticalSpacing);
        var rightYs = GenerateRandomSpacedValues(rightCount, spawnYRange.x, spawnYRange.y, minVerticalSpacing);

        // Left column
        foreach (float y in leftYs)
        {
            float baseX = leftX;
            bool placed = false;

            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                float x = baseX;
                if (randomizeColumnX && columnXJitter > 0f)
                {
                    x += Random.Range(-columnXJitter, columnXJitter);
                    x = Mathf.Clamp(x, minX, maxX);
                }

                float yCandidate = y + Random.Range(-minVerticalSpacing * 0.5f, minVerticalSpacing * 0.5f);
                yCandidate = Mathf.Clamp(yCandidate, spawnYRange.x, spawnYRange.y);

                var pos = new Vector3(x, yCandidate, z);

                if (IsAreaFree(pos, lensPlacementClearance))
                {
                    var go = Instantiate(lensPrefab, pos, Quaternion.identity, transform);
                    go.name = $"{lensPrefab.name}_L";

                    // Ensure lens Rigidbody is set up correctly
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb == null)
                        rb = go.AddComponent<Rigidbody>();

                    rb.constraints = RigidbodyConstraints.FreezePositionZ
                                   | RigidbodyConstraints.FreezeRotationX
                                   | RigidbodyConstraints.FreezeRotationY;

                    RegisterLens(go);
                    placed = true;
                    break;
                }
            }

            if (!placed)
                Debug.LogWarning($"LensesController: Could not place lens in left column at base Y={y} after {maxPlacementAttempts} attempts.");
        }

        // Right column
        foreach (float y in rightYs)
        {
            float baseX = rightX;
            bool placed = false;

            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                float x = baseX;
                if (randomizeColumnX && columnXJitter > 0f)
                {
                    x += Random.Range(-columnXJitter, columnXJitter);
                    x = Mathf.Clamp(x, minX, maxX);
                }

                float yCandidate = y + Random.Range(-minVerticalSpacing * 0.5f, minVerticalSpacing * 0.5f);
                yCandidate = Mathf.Clamp(yCandidate, spawnYRange.x, spawnYRange.y);

                var pos = new Vector3(x, yCandidate, z);

                if (IsAreaFree(pos, lensPlacementClearance))
                {
                    var go = Instantiate(lensPrefab, pos, Quaternion.identity, transform);
                    go.name = $"{lensPrefab.name}_R";

                    var rb = go.GetComponent<Rigidbody>();
                    if (rb == null)
                        rb = go.AddComponent<Rigidbody>();

                    rb.constraints = RigidbodyConstraints.FreezePositionZ
                                   | RigidbodyConstraints.FreezeRotationX
                                   | RigidbodyConstraints.FreezeRotationY;

                    RegisterLens(go);
                    placed = true;
                    break;
                }
            }

            if (!placed)
                Debug.LogWarning($"LensesController: Could not place lens in right column at base Y={y} after {maxPlacementAttempts} attempts.");
        }
    }

    private List<float> GenerateRandomSpacedValues(int count, float min, float max, float minDistance)
    {
        var result = new List<float>(count);
        if (count <= 0) return result;

        int attempts = 0;
        int maxAttemptsLocal = Mathf.Max(1000, count * 200);

        while (result.Count < count && attempts < maxAttemptsLocal)
        {
            float candidate = Random.Range(min, max);

            bool ok = true;
            foreach (float r in result)
            {
                if (Mathf.Abs(r - candidate) < minDistance)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
                result.Add(candidate);

            attempts++;
        }

        if (result.Count < count)
        {
            result.Clear();
            float total = Mathf.Max(0.0001f, max - min);

            if (count == 1)
            {
                result.Add(min + total * 0.5f);
            }
            else
            {
                float step = total / (count - 1);
                for (int i = 0; i < count; i++)
                    result.Add(min + step * i);
            }
        }

        return result;
    }

    #endregion

    #region Selection

    public void ActivateMinigame()
    {
        // hook if you want to gate Update()
    }

    public void SelectLens(Lens lens)
    {
        if (lens == currentSelected)
        {
            if (currentSelected != null)
                currentSelected.ToggleSelected();

            currentSelected = null;
            return;
        }

        if (currentSelected != null)
            currentSelected.ToggleSelected();

        currentSelected = lens;
        currentSelected?.ToggleSelected();
    }

    public void DeselectAll()
    {
        if (currentSelected != null && currentSelected.isSelected)
            currentSelected.ToggleSelected();

        currentSelected = null;
    }

    #endregion

    #region Laser / Bounce

    private void CastAndRenderBeam()
    {
        if (emitter == null || line == null)
            return;

        Vector3 origin = emitter.position;
        Vector3 dir = (startDownward ? Vector3.down : emitter.forward).normalized;

        var points = new List<Vector3>(maxBounces) { origin };

        float remaining = Mathf.Max(0f, maxDistance);
        int bounces = 0;

        while (bounces <= maxBounces && remaining > 0f)
        {
            var ray = new Ray(origin, dir);

            if (Physics.Raycast(ray, out RaycastHit hit, remaining, rayMask, QueryTriggerInteraction.Ignore))
            {
                float traveled = Mathf.Max(hit.distance, 0f);
                if (traveled < MIN_SEGMENT)
                {
                    origin += dir * SURFACE_OFFSET;
                    remaining -= SURFACE_OFFSET;
                    bounces++;
                    continue;
                }

                points.Add(hit.point);
                remaining -= traveled;

                if (hit.collider.CompareTag("Target"))
                {
                    Invoke("CompletePuzzle", 2f);
                    break;
                }

                Lens lens = hit.collider.GetComponentInParent<Lens>();
                bool doReflect = lens != null && lens.isReflective;

                if (doReflect && remaining > 0f)
                {
                    dir = Vector3.Reflect(dir, hit.normal).normalized;
                    origin = hit.point + dir * SURFACE_OFFSET;
                    bounces++;
                    continue;
                }

                break;
            }
            else
            {
                points.Add(origin + dir * remaining);
                break;
            }
        }

        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            line.SetPosition(i, points[i]);
    }

    private void CompletePuzzle()
    {
        if (completed) return;

        completed = true;
        onPuzzleComplete?.Invoke();

        if (line != null)
            line.gameObject.SetActive(false);

        // Hide the in-game quit button and show the completion options
        if (quitButton != null)
            quitButton.SetActive(false);

        ShowCompletionPanel();
        DeselectAll();
    }

    private void ShowCompletionPanel()
    {
        if (completionPanel != null)
            completionPanel.SetActive(true);
    }

    public void ResetPuzzle()
    {
        DeselectAll();

        foreach (var g in lenses)
        {
            if (g == null) continue;
            var lens = g.GetComponent<Lens>();
            lens?.ResetRotation();
        }

        if (line != null)
        {
            line.gameObject.SetActive(true);

            if (initialLinePositions != null && initialLinePositions.Length > 0)
            {
                line.positionCount = initialLinePositions.Length;
                line.SetPositions(initialLinePositions);
            }
            else if (emitter != null)
            {
                line.positionCount = 1;
                line.SetPosition(0, emitter.position);
            }
        }

        if (completionPanel != null)
            completionPanel.SetActive(false);

        if (quitButton != null)
            quitButton.SetActive(true);

        completed = false;
    }

    #endregion

    /// <summary>
    /// Check whether any physics collider exists within a sphere of 'clearance' around 'position'.
    /// Uses spawnBlockMask so only selected layers block placement.
    /// </summary>
    private bool IsAreaFree(Vector3 position, float clearance)
    {
        if (clearance <= 0f)
            return true;

        var hits = Physics.OverlapSphere(position, clearance, spawnBlockMask, QueryTriggerInteraction.Ignore);
        return hits == null || hits.Length == 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        float z = emitter != null ? emitter.position.z : 0f;

        Gizmos.color = Color.cyan;

        if (spawnInTwoColumns)
        {
            Vector3 leftTop = new Vector3(spawnXRange.x, spawnYRange.y, z);
            Vector3 leftBottom = new Vector3(spawnXRange.x, spawnYRange.x, z);
            Vector3 rightTop = new Vector3(spawnXRange.y, spawnYRange.y, z);
            Vector3 rightBottom = new Vector3(spawnXRange.y, spawnYRange.x, z);

            Gizmos.DrawLine(leftBottom, leftTop);
            Gizmos.DrawLine(rightBottom, rightTop);
            Gizmos.DrawSphere(leftTop, 0.05f);
            Gizmos.DrawSphere(leftBottom, 0.05f);
            Gizmos.DrawSphere(rightTop, 0.05f);
            Gizmos.DrawSphere(rightBottom, 0.05f);
        }
        else
        {
            float midY = (spawnYRange.x + spawnYRange.y) * 0.5f;
            Vector3 a = new Vector3(spawnXRange.x, midY, z);
            Vector3 b = new Vector3(spawnXRange.y, midY, z);

            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.05f);
            Gizmos.DrawSphere(b, 0.05f);
        }
    }
}
