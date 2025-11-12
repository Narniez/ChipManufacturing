using UnityEngine;

public class SawCutter : MonoBehaviour
{
    public Transform sawBlade;
    public Collider sawCollider;              // assign the Saw's Collider
    public MeshCollider plateCollider;
    public Renderer plateRenderer;            // Plate's MeshRenderer
    public Material plateMaterial;            // Custom/PlateCutReveal
    public Material paintMaterial;            // Custom/CutPaint
    public RenderTexture maskAsset;           // optional (used as the "read" RT)

    [Range(0.001f, 1f)]
    public float brushWorldRadius = 0.05f;

    [Range(0f, 1f)]
    public float hardness = 0.8f;

    [Min(64)]
    public int maskResolution = 1024;

    [Range(0.001f, 0.5f)]
    public float maxPaintSeparation = 0.06f;

    public bool invertV = false;

    RenderTexture _maskRead;   // the texture the plate samples
    RenderTexture _maskWrite;  // the texture we render into this frame
    Material _paintMat;

    Bounds _meshBounds; // renderer local bounds (Unity Plane: ~min(-5,0,-5), size(10,0,10))

    // Force Unity Plane axes: U = X, V = Z
    const int _uAxis = 0;
    const int _vAxis = 2;

    static readonly int CutMaskId     = Shader.PropertyToID("_CutMask");
    static readonly int PrevMaskId    = Shader.PropertyToID("_PrevMask");
    static readonly int CenterId      = Shader.PropertyToID("_Center");
    static readonly int RadiusId      = Shader.PropertyToID("_Radius");
    static readonly int HardnessId    = Shader.PropertyToID("_Hardness");
    static readonly int PlateUVMinId  = Shader.PropertyToID("_PlateUVMin");
    static readonly int PlateUVSizeId = Shader.PropertyToID("_PlateUVSize");
    static readonly int UAxisMaskId   = Shader.PropertyToID("_UAxisMask");
    static readonly int VAxisMaskId   = Shader.PropertyToID("_VAxisMask");
    static readonly int InvertVId     = Shader.PropertyToID("_InvertV");

    void Start()
    {
        if (plateRenderer != null)
            plateMaterial = plateRenderer.material;

        if (plateRenderer != null)
        {
            var mf = plateRenderer.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh)
                _meshBounds = mf.sharedMesh.bounds;
        }

        // Create/adopt read RT
        _maskRead = maskAsset != null ? maskAsset : CreateMaskRT();
        ClearRT(_maskRead, Color.white);
        // Create write RT (always created, same spec)
        _maskWrite = CreateMaskRT();
        ClearRT(_maskWrite, Color.white);

        if (plateMaterial != null)
        {
            plateMaterial.SetTexture(CutMaskId, _maskRead);
            PushPlateMappingToMaterial();
        }

        _paintMat = paintMaterial != null
            ? new Material(paintMaterial)
            : new Material(Shader.Find("Custom/CutPaint"));
    }

    RenderTexture CreateMaskRT()
    {
        var rtFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
            ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;

        var rt = new RenderTexture(maskResolution, maskResolution, 0, rtFormat)
        {
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        rt.Create();
        return rt;
    }

    void ClearRT(RenderTexture rt, Color color)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(false, true, color);
        RenderTexture.active = prev;
    }

    void Update()
    {
        if (!plateCollider || !plateRenderer) return;

        Vector3 from = sawBlade ? sawBlade.position : transform.position;
        Vector3 toPlate = plateCollider.bounds.ClosestPoint(from) - from;
        Vector3 dir = toPlate.sqrMagnitude > 1e-6f ? toPlate.normalized : -plateCollider.transform.forward;

        if (!plateCollider.Raycast(new Ray(from, dir), out var hit, 100f) &&
            !plateCollider.Raycast(new Ray(from, -dir), out hit, 100f))
            return;

        if (!IsNearPlateSurface(hit))
            return;

        // Map contact point to plate renderer local XZ bounds (0..1)
        Vector2 uv = WorldToRendererBoundsUV(hit.point);
        float uvRadius = WorldToUVRadius(brushWorldRadius);

        // Stamp by reading from _maskRead and writing to _maskWrite
        _paintMat.SetTexture(PrevMaskId, _maskRead);
        _paintMat.SetVector(CenterId, uv);
        _paintMat.SetFloat(RadiusId, uvRadius);
        _paintMat.SetFloat(HardnessId, Mathf.Clamp01(hardness));

        // Important: read=_maskRead, write=_maskWrite (ping-pong)
        Graphics.Blit(_maskRead, _maskWrite, _paintMat);

        // Swap
        var tmp = _maskRead;
        _maskRead = _maskWrite;
        _maskWrite = tmp;

        // Ensure plate samples the latest mask
        if (plateMaterial != null)
            plateMaterial.SetTexture(CutMaskId, _maskRead);
    }

    bool IsNearPlateSurface(RaycastHit hit)
    {
        if (!sawCollider)
        {
            float approx = Vector3.Distance(hit.point, (sawBlade ? sawBlade.position : transform.position));
            return approx <= maxPaintSeparation * 5f;
        }

        Vector3 closest = Physics.ClosestPoint(hit.point, sawCollider, sawCollider.transform.position, sawCollider.transform.rotation);
        float separation = Vector3.Distance(closest, hit.point);
        return separation <= maxPaintSeparation;
    }

    // Map world contact to renderer-local X/Z bounds 0..1
    Vector2 WorldToRendererBoundsUV(Vector3 worldPoint)
    {
        var tf = plateRenderer.transform;
        Vector3 p = tf.InverseTransformPoint(worldPoint);
        Vector3 min = _meshBounds.min;
        Vector3 size = _meshBounds.size;

        float u = Mathf.InverseLerp(min.x, min.x + size.x, p.x);
        float v = Mathf.InverseLerp(min.z, min.z + size.z, p.z);
        if (invertV) v = 1f - v;
        return new Vector2(Mathf.Repeat(u, 1f), Mathf.Repeat(v, 1f));
    }

    float WorldToUVRadius(float worldR)
    {
        var tf = plateRenderer.transform;
        Vector3 lossy = tf.lossyScale;
        float scaleU = Mathf.Abs(lossy.x);
        float scaleV = Mathf.Abs(lossy.z);
        float avgScale = (scaleU + scaleV) * 0.5f;
        float localR = worldR / Mathf.Max(1e-5f, avgScale);
        float denom = Mathf.Max(_meshBounds.size.x, _meshBounds.size.z);
        return localR / Mathf.Max(1e-5f, denom);
    }

    void PushPlateMappingToMaterial()
    {
        if (plateMaterial == null) return;
        Vector3 min = _meshBounds.min;
        Vector3 size = _meshBounds.size;

        // U = X, V = Z (Unity Plane)
        plateMaterial.SetVector(PlateUVMinId,   new Vector4(min.x,  min.y,  min.z,  0f));
        plateMaterial.SetVector(PlateUVSizeId,  new Vector4(size.x, size.y, size.z, 0f));
        plateMaterial.SetVector(UAxisMaskId,    new Vector4(1f, 0f, 0f, 0f));
        plateMaterial.SetVector(VAxisMaskId,    new Vector4(0f, 0f, 1f, 0f));
        plateMaterial.SetFloat(InvertVId, invertV ? 1f : 0f);
    }

    void OnDestroy()
    {
        if (_maskWrite && _maskWrite != maskAsset) _maskWrite.Release();
        if (_maskRead  && _maskRead  != maskAsset) _maskRead.Release();
        if (plateMaterial != null) plateMaterial.SetTexture(CutMaskId, null);
    }
}