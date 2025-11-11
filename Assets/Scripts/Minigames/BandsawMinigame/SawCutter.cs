using UnityEngine;

public class SawCutter : MonoBehaviour
{
    public Transform sawBlade;
    public MeshCollider plateCollider;
    public Renderer plateRenderer;         // Assign the visible plate's MeshRenderer
    public Material plateMaterial;         // Uses Custom/PlateCutReveal
    public Material paintMaterial;         // Uses Custom/CutPaint
    public RenderTexture maskAsset;        // Optional: assign a RT asset for debugging/UI

    [Range(0.001f, 1f)]
    public float brushWorldRadius = 0.05f;

    [Range(0f, 1f)]
    public float hardness = 0.8f;

    [Min(64)]
    public int maskResolution = 1024;

    // If holes appear vertically mirrored on the plate, enable this
    public bool invertV = false;

    RenderTexture _mask;
    Material _paintMat;

    static readonly int CutMaskId   = Shader.PropertyToID("_CutMask");
    static readonly int CenterId    = Shader.PropertyToID("_Center");
    static readonly int RadiusId    = Shader.PropertyToID("_Radius");
    static readonly int HardnessId  = Shader.PropertyToID("_Hardness");

    void Start()
    {
        // Use the exact runtime instance the renderer is drawing with
        if (plateRenderer != null)
            plateMaterial = plateRenderer.material;

        // Use RT asset (visible in RawImage) or create one
        if (maskAsset != null)
        {
            _mask = maskAsset;
            var prev = RenderTexture.active;
            RenderTexture.active = _mask;
            GL.Clear(false, true, Color.white);
            RenderTexture.active = prev;
        }
        else
        {
            var rtFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;

            _mask = new RenderTexture(maskResolution, maskResolution, 0, rtFormat)
            {
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _mask.Create();

            var prev = RenderTexture.active;
            RenderTexture.active = _mask;
            GL.Clear(false, true, Color.white);
            RenderTexture.active = prev;
        }

        if (plateMaterial != null)
            plateMaterial.SetTexture(CutMaskId, _mask);

        _paintMat = paintMaterial != null
            ? new Material(paintMaterial)
            : new Material(Shader.Find("Custom/CutPaint"));
    }

    void Update()
    {
        if (!sawBlade || !plateCollider) return;

        // Ray toward plate bounds for robustness
        Vector3 toPlate = plateCollider.bounds.ClosestPoint(sawBlade.position) - sawBlade.position;
        Vector3 dir = toPlate.sqrMagnitude > 1e-6f ? toPlate.normalized : -plateCollider.transform.forward;

        if (plateCollider.Raycast(new Ray(sawBlade.position, dir), out var hit, 100f) ||
            plateCollider.Raycast(new Ray(sawBlade.position, -dir), out hit, 100f))
        {
            Vector2 uv = hit.textureCoord;
            if (invertV) uv.y = 1f - uv.y;

            float uvRadius = WorldToUVRadius(brushWorldRadius, plateCollider);
            Stamp(uv, uvRadius);
        }
    }

    float WorldToUVRadius(float worldR, MeshCollider col)
    {
        var mesh = col.sharedMesh;
        var bounds = mesh.bounds;
        Vector3 lossy = col.transform.lossyScale;
        float avgScale = (lossy.x + lossy.z) * 0.5f;
        float localR = worldR / Mathf.Max(1e-5f, avgScale);
        return localR / Mathf.Max(1e-5f, bounds.size.x); // Unity Plane local width ~10
    }

    void Stamp(Vector2 uv, float uvRadius)
    {
        float h = Mathf.Clamp01(hardness);

        _paintMat.SetVector(CenterId, uv);
        _paintMat.SetFloat(RadiusId, uvRadius);
        _paintMat.SetFloat(HardnessId, h);

        Graphics.Blit(null, _mask, _paintMat);
    }

    void OnDestroy()
    {
        if (_mask && _mask != maskAsset) _mask.Release();
        if (plateMaterial != null) plateMaterial.SetTexture(CutMaskId, null);
    }
}
