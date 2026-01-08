using System.Collections;
using UnityEngine;

public class ScenarioA_Runner : MonoBehaviour
{
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private float refreshHz = 60f;

    private BeltChainPreviewController _controller;
    private ConveyorBelt _tail;
    private float _accum;

    private IEnumerator Start()
    {
        if (placementManager == null) placementManager = PlacementManager.Instance;
        if (placementManager == null) { Debug.LogError("[ScenarioA] No PlacementManager."); yield break; }

        // Wait grid ready
        while (placementManager.GridService == null || !placementManager.GridService.HasGrid)
            yield return null;

        // Wait belt exists (save load)
        while (_tail == null)
        {
            _tail = FindAnyObjectByType<ConveyorBelt>();
            yield return null;
        }

        _controller = new BeltChainPreviewController(placementManager);

        Debug.Log($"[ScenarioA] Tail={_tail.name} anchor={_tail.Anchor} ori={_tail.Orientation}");
        _controller.ShowOptionsFrom(_tail);
    }

    private void Update()
    {
        if (_controller == null || _tail == null) return;

        float interval = 1f / Mathf.Max(1f, refreshHz);
        _accum += Time.unscaledDeltaTime;

        while (_accum >= interval)
        {
            _accum -= interval;
            _controller.ShowOptionsFrom(_tail); // THIS is the churn
        }
    }

    private void OnDisable()
    {
        _controller?.Cleanup();
    }
}
