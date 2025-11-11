using UnityEngine;

public class SawCutController : MonoBehaviour
{
    [SerializeField] private Material plateMaterial;
    [SerializeField] private string cutMaskProperty = "_CutMask";
    [SerializeField] private RenderTexture cutMaskRT;
    [SerializeField] private Camera maskCamera;

    private bool isCutting;

    private void Start()
    {
        if (plateMaterial != null)
            plateMaterial.SetTexture(cutMaskProperty, cutMaskRT);
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Plate"))
        {
            isCutting = true;
            DrawCutAtSawPosition();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Plate"))
            isCutting = false;
    }

    void DrawCutAtSawPosition()
    {
        if (maskCamera == null || cutMaskRT == null) return;

        // Project saw position onto RenderTexture
        Vector3 screenPos = maskCamera.WorldToViewportPoint(transform.position);
        Vector2 uv = new Vector2(screenPos.x, screenPos.y);

        // Draw to mask RT via shader (see below)
        Shader.SetGlobalVector("_CutPosition", new Vector4(uv.x, uv.y, 0, 0));
    }
}
