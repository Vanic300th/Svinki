using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FlashlightController : MonoBehaviour
{
    [SerializeField] private bool startOn = true;
    [SerializeField, Tooltip("HUD этой сцены (префаб GameHUD). Пусто — найдётся сам")]
    private GameHud hud;

    private Light flashlight;
    private bool isOn;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Transform model;
    private GrayboxPlayerController motor;
    private float lowered, stride;
    private static Material bodyMaterial, lensMaterial;
    public bool IsOn => isOn;
    public void SetOn(bool value) { isOn = value; if (flashlight != null) flashlight.enabled = value; }

    private void Awake()
    {
        flashlight = GetComponent<Light>() ?? GetComponentInChildren<Light>();
        if (flashlight != null) ConfigureLight(flashlight);
        restPosition = transform.localPosition; restRotation = transform.localRotation;
        isOn = startOn;
        if (flashlight != null) flashlight.enabled = isOn;
    }

    public static void ConfigureLight(Light light)
    {
        light.type = LightType.Spot; light.color = new Color(1f, .95f, .82f);
        light.range = 18; light.spotAngle = 76; light.innerSpotAngle = 42;
        light.intensity = 84; light.shadows = LightShadows.Soft;
        light.shadowStrength = .9f; light.shadowBias = .015f; light.shadowNormalBias = .08f;
        light.cullingMask = ~(1 << 5);
    }

    private void LateUpdate()
    {
        UpdateHud();
        Camera camera = GetComponentInParent<Camera>();
        if (camera == null) return;
        if (motor == null)
            foreach (PlayerAvatar player in PlayerRegistry.Players)
                if (player != null && player.IsLocal) { motor = player.GetComponent<GrayboxPlayerController>(); break; }
        if (model == null)
        {
            model = CreateModel(transform, "First Person Flashlight");
            model.localPosition = new Vector3(-.36f, -.27f, .12f);
            int layer = LayerMask.NameToLayer("FirstPersonBody");
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer >= 0 ? layer : 0;
        }
        float speed = motor != null ? motor.MotionSpeed : 0;
        stride += Time.deltaTime * Mathf.Lerp(3, 11, Mathf.Clamp01(speed / 10));
        lowered = Mathf.MoveTowards(lowered, isOn ? 0 : 1, Time.deltaTime * 5);
        float bob = Mathf.Clamp01(speed / 5) * .006f;
        Vector2 mouse = !PlayerChat.BlocksInput && !EmoteWheel.BlocksInput && Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Vector3 sway = new Vector3(Mathf.Clamp(-mouse.x * .00015f, -.015f, .015f),
            Mathf.Sin(stride) * bob - lowered * .045f, 0);
        transform.localPosition = Vector3.Lerp(transform.localPosition, restPosition + sway, Time.deltaTime * 12);
        Quaternion target = restRotation * Quaternion.Euler(lowered * 35 + Mathf.Clamp(mouse.y * .015f, -2, 2),
            Mathf.Clamp(-mouse.x * .015f, -2, 2), Mathf.Sin(stride * .5f) * bob * 80);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * 12);
    }

    public static Transform CreateModel(Transform parent, string name)
    {
        if (bodyMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            bodyMaterial = new Material(shader) { name = "Flashlight body" };
            bodyMaterial.SetColor("_BaseColor", new Color(.06f, .075f, .08f));
            bodyMaterial.SetFloat("_Metallic", .65f); bodyMaterial.SetFloat("_Smoothness", .55f);
            lensMaterial = new Material(bodyMaterial) { name = "Flashlight lens" };
            lensMaterial.SetColor("_BaseColor", new Color(.6f, .65f, .7f));
        }
        var root = new GameObject(name).transform; root.SetParent(parent, false);
        MakePart(root, "Grip", new Vector3(0, 0, 0), new Vector3(.055f, .09f, .055f), bodyMaterial);
        MakePart(root, "Lamp", new Vector3(0, 0, .11f), new Vector3(.075f, .025f, .075f), lensMaterial);
        return root;
    }

    private static void MakePart(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder); obj.name = name;
        obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
        obj.transform.localRotation = Quaternion.Euler(90, 0, 0); obj.transform.localScale = scale;
        var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMaterials()
    {
        if (bodyMaterial != null) Destroy(bodyMaterial);
        if (lensMaterial != null) Destroy(lensMaterial);
        bodyMaterial = null; lensMaterial = null;
    }

    private void Update()
    {
        if (EmoteWheel.BlocksInput || PlayerChat.BlocksInput || NetworkLobby.Instance != null && !NetworkLobby.Instance.InputAllowed) return;
        if (Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame) return;

        isOn = !isOn;
        if (flashlight != null) flashlight.enabled = isOn;
    }

    // Статус фонарика рисует HUD (префаб GameHUD).
    private void UpdateHud()
    {
        if (hud == null) hud = GameHud.Find(this);
        if (hud == null) return;
        var lobby = NetworkLobby.Instance;
        hud.SetFlashlight(lobby == null || lobby.InputAllowed && !lobby.MenuVisible, isOn);
    }

    private void OnDisable()
    {
        if (hud != null) hud.SetFlashlight(false, isOn);
    }
}
