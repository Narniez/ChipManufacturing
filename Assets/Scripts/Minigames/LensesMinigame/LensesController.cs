using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class LensesController : MonoBehaviour
{
    [Header("Lens Settings")]
    [SerializeField] private Transform emitter;
    [SerializeField] private LineRenderer line;
    [SerializeField] private float emissionSpeed;
    [SerializeField] private int maxBounces = 8;
    [SerializeField] private float maxDistance = 50f;

    [SerializeField] private List<GameObject> lenses = new();

    [Header("Completion")]
    public UnityEvent onPuzzleComplete;

    [Header("Lenses Discovery")]
    [Tooltip("Auto-find all Lens components in the scene at Start.")]
    public bool autoFindLenses = true;

    private bool isMinigameActive = false;
    private bool isLenshit = false;

    const float SURFACE_OFFSET = 0.001f;
    bool completed;

    private void Awake()
    {
        foreach (var lens in lenses)
        {
            lens.SetActive(true);
        }
    }

    public void RegisterLens(GameObject lens)
    {
        lenses.Add(lens);
    }

    public void ActivateMinigame()
    {
        isMinigameActive = true;
    }


}
