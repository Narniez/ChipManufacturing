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

        // waiting for the grid to be ready
        while (placementManager.GridService == null || !placementManager.GridService.HasGrid)
            yield return null;

        // waiting for belt to exist (save load)
        while (_tail == null)
        {
            _tail = FindAnyObjectByType<ConveyorBelt>();
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1f);

        _controller = new BeltChainPreviewController(placementManager, prewarmCount: 6, maxPoolSize: 32);

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
            _controller.ShowOptionsFrom(_tail); 
        }
    }

    private void OnDisable()
    {
        _controller?.Cleanup();
    }
}
