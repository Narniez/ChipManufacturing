using UnityEngine;
using UnityEngine.UI;

public class FollowMachine : MonoBehaviour
{
    private GameObject target;
    private Camera cam;
    private RectTransform rectTransform;

    private void Awake()
    {
        cam = Camera.main;
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        TutorialEventBus.OnPreviewStarted += HandlePreviewStarted;
        TutorialEventBus.OnPreviewConfirmed += HandlePreviewEnded;
        TutorialEventBus.OnSelectionChanged += HandleSelectionChanged;

    }

    private void OnDisable()
    {
        TutorialEventBus.OnPreviewStarted -= HandlePreviewStarted;
        TutorialEventBus.OnPreviewConfirmed -= HandlePreviewEnded;
        TutorialEventBus.OnSelectionChanged -= HandleSelectionChanged;

    }

    private void Update()
    {
        if (target == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(target.transform.position);
        rectTransform.position = screenPos;
    }

    private void HandlePreviewStarted(GameObject previewObject)
    {
        target = previewObject;
    }

    private void HandlePreviewEnded()
    {
        target = null;
    }

    private void HandleSelectionChanged(IGridOccupant occ)
    {
        if (occ == null)
        {
            target = null;
            return;
        }

        var comp = occ as Component;
        if (comp == null) return;

        target = comp.gameObject;
    }

}
