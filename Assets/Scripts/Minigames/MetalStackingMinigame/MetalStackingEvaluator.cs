using UnityEngine;
using UnityEngine.Events;

public class MetalStackingEvaluator : MonoBehaviour
{
    [Header("Goal")]
    [SerializeField] private int targetLayers = 10;

    private UnityEvent onWin;
    private UnityEvent onFail;
    private UnityEvent<int> onLayerCountChanged;

    private bool _finished;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnLayerCompleted(int totalLayers)
    {
        if (_finished)
        {
            return;
        }

        onLayerCountChanged?.Invoke(totalLayers);

        if (totalLayers >= targetLayers)
        {
            _finished = true;
            onWin?.Invoke();
            Debug.Log($"MetalStacking: Win! Layers={totalLayers}/{targetLayers}");
        }
    }

    public void Fail()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        onFail?.Invoke();
        Debug.Log("MetalStacking: Fail.");
    }

    public void ResetRun()
    {
        _finished = false;
        onLayerCountChanged?.Invoke(0);
    }

    public void SetTargetLayers(int layers)
    {
        targetLayers = Mathf.Max(1, layers);
    }
}
