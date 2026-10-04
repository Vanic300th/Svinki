using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Отражает камеру игрока относительно плоскости зеркала и выводит результат на её поверхность.</summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class PlanarMirror : MonoBehaviour
{
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Renderer mirrorSurface;
    [SerializeField, Range(0.25f, 1f)] private float resolutionScale = 0.6f;
    [SerializeField, Min(0f)] private float clipPlaneOffset = 0.03f;

    private Camera reflectionCamera;
    private RenderTexture reflectionTexture;
    private MaterialPropertyBlock surfaceProperties;
    private bool previousCulling;
    private bool renderingReflection;

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
        if (Application.isPlaying) Initialize();
    }

    private void Start() => Initialize();

    private void Initialize()
    {
        if (Application.isBatchMode || mirrorSurface == null || reflectionCamera != null) return;
        if (sourceCamera == null) sourceCamera = Camera.main;
        if (sourceCamera == null) return;

        GameObject cameraObject = new GameObject("Mirror Reflection Camera");
        cameraObject.transform.SetParent(transform, false);
        reflectionCamera = cameraObject.AddComponent<Camera>();
        reflectionCamera.enabled = false;

        surfaceProperties = new MaterialPropertyBlock();
        mirrorSurface.GetPropertyBlock(surfaceProperties);
    }

    private void LateUpdate()
    {
        if (reflectionCamera == null || sourceCamera == null) return;
        Vector3 normal = transform.forward;
        bool inFront = Vector3.Dot(sourceCamera.transform.position - transform.position, normal) > 0.05f;
        if (!sourceCamera.isActiveAndEnabled || !inFront)
        {
            reflectionCamera.enabled = false;
            return;
        }

        EnsureTexture();
        reflectionCamera.CopyFrom(sourceCamera);
        reflectionCamera.depth = sourceCamera.depth - 100f;
        reflectionCamera.targetTexture = reflectionTexture;
        int localPlayerLayer = LayerMask.NameToLayer("LocalPlayerMirror");
        int mirrorLayer = LayerMask.NameToLayer("MirrorSurface");
        int mask = sourceCamera.cullingMask & ~(1 << 5); // HUD находится на слое UI.
        if (localPlayerLayer >= 0) mask |= 1 << localPlayerLayer;
        if (mirrorLayer >= 0) mask &= ~(1 << mirrorLayer);
        reflectionCamera.cullingMask = mask;

        Vector3 sourcePosition = sourceCamera.transform.position;
        Vector3 reflectedPosition = sourcePosition - 2f * Vector3.Dot(sourcePosition - transform.position, normal) * normal;
        Vector3 reflectedForward = Vector3.Reflect(sourceCamera.transform.forward, normal);
        Vector3 reflectedUp = Vector3.Reflect(sourceCamera.transform.up, normal);
        reflectionCamera.transform.SetPositionAndRotation(reflectedPosition,
            Quaternion.LookRotation(reflectedForward, reflectedUp));

        float distance = -Vector3.Dot(normal, transform.position);
        Matrix4x4 reflection = ReflectionMatrix(new Vector4(normal.x, normal.y, normal.z, distance));
        reflectionCamera.worldToCameraMatrix = sourceCamera.worldToCameraMatrix * reflection;
        reflectionCamera.projectionMatrix = sourceCamera.projectionMatrix;
        Vector4 clipPlane = CameraSpacePlane(transform.position, normal);
        reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);
        reflectionCamera.enabled = true;
    }

    private void EnsureTexture()
    {
        int sourceWidth = Mathf.Max(1, sourceCamera.pixelWidth);
        int sourceHeight = Mathf.Max(1, sourceCamera.pixelHeight);
        float scale = Mathf.Min(resolutionScale, 1024f / Mathf.Max(sourceWidth, sourceHeight));
        int width = Mathf.Max(128, Mathf.RoundToInt(sourceWidth * scale));
        int height = Mathf.Max(128, Mathf.RoundToInt(sourceHeight * scale));
        if (reflectionTexture != null && reflectionTexture.width == width && reflectionTexture.height == height)
            return;

        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            Release(reflectionTexture);
        }
        reflectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "Player Mirror Reflection",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false
        };
        reflectionTexture.Create();
        surfaceProperties.SetTexture("_ReflectionTex", reflectionTexture);
        mirrorSurface.SetPropertyBlock(surfaceProperties);
    }

    private Vector4 CameraSpacePlane(Vector3 point, Vector3 normal)
    {
        Matrix4x4 view = reflectionCamera.worldToCameraMatrix;
        Vector3 position = view.MultiplyPoint(point + normal * clipPlaneOffset);
        Vector3 direction = view.MultiplyVector(normal).normalized;
        return new Vector4(direction.x, direction.y, direction.z, -Vector3.Dot(position, direction));
    }

    private static Matrix4x4 ReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.m00 = 1f - 2f * plane.x * plane.x;
        matrix.m01 = -2f * plane.x * plane.y;
        matrix.m02 = -2f * plane.x * plane.z;
        matrix.m03 = -2f * plane.w * plane.x;
        matrix.m10 = -2f * plane.y * plane.x;
        matrix.m11 = 1f - 2f * plane.y * plane.y;
        matrix.m12 = -2f * plane.y * plane.z;
        matrix.m13 = -2f * plane.w * plane.y;
        matrix.m20 = -2f * plane.z * plane.x;
        matrix.m21 = -2f * plane.z * plane.y;
        matrix.m22 = 1f - 2f * plane.z * plane.z;
        matrix.m23 = -2f * plane.w * plane.z;
        return matrix;
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != reflectionCamera) return;
        previousCulling = GL.invertCulling;
        GL.invertCulling = !previousCulling;
        renderingReflection = true;
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != reflectionCamera || !renderingReflection) return;
        GL.invertCulling = previousCulling;
        renderingReflection = false;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        if (renderingReflection) GL.invertCulling = previousCulling;
        renderingReflection = false;
        Cleanup();
    }

    private void OnDestroy() => Cleanup();

    private void Cleanup()
    {
        if (reflectionCamera != null)
        {
            reflectionCamera.enabled = false;
            Release(reflectionCamera.gameObject);
            reflectionCamera = null;
        }
        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            Release(reflectionTexture);
            reflectionTexture = null;
        }
        if (mirrorSurface != null) mirrorSurface.SetPropertyBlock(null);
        surfaceProperties = null;
    }

    private static void Release(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
