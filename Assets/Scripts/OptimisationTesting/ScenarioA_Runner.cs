using UnityEngine;

/// <summary>
/// Scenario A: repeatedly refresh belt-chain ghost options to generate short-lived object churn
/// </summary>
public class ScenarioA_Runner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlacementManager placementManager;

    [Header("Workload")]
    [SerializeField] private float refreshHz = 30f; // 30 or 60
    [SerializeField] private bool startOnPlay = true;

    private BeltChainPreviewController _controller;
    private ConveyorBelt tailBelt; // a straight belt in free space
    private float _accum;

    void Start()
    {
        if (!startOnPlay) return;

        if (placementManager == null) placementManager = PlacementManager.Instance;
        if (placementManager == null)
        {
            Debug.LogError("[ScenarioA] PlacementManager missing.");
            enabled = false;
            return;
        }

        if (tailBelt == null)
        {
            tailBelt = FindAnyObjectByType<ConveyorBelt>();
            //Debug.LogError("[ScenarioA] Assign a straight ConveyorBelt as tailBelt (placed on the grid).");
            enabled = true;
        }

        _controller = new BeltChainPreviewController(placementManager);

        // First spawn so we know it works
        _controller.ShowOptionsFrom(tailBelt);
    }

    void Update()
    {
        if (_controller == null || tailBelt == null) return;

        float interval = refreshHz <= 0 ? 0.033f : (1f / refreshHz);
        _accum += Time.unscaledDeltaTime;

        while (_accum >= interval)
        {
            _accum -= interval;
            _controller.ShowOptionsFrom(tailBelt); // churn: cleanup + spawn ghosts again
        }
    }

    void OnDisable()
    {
        _controller?.Cleanup();
    }
}
