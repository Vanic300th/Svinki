using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class PickupItem : MonoBehaviour
{
    public enum PickupAction { DisableObject, DestroyObject }

    [Header("Pickup")]
    [SerializeField] private string itemName = "Предмет";
    [SerializeField] private PickupAction afterPickup = PickupAction.DisableObject;
    [SerializeField] private UnityEvent onPickedUp = new UnityEvent();

    [Header("Yellow highlight")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.78f, 0.06f);
    [SerializeField, Min(0f)] private float glowStrength = 2.5f;
    [SerializeField, Min(0f)] private float sparkleRate = 16f;

    private const string SparkleObjectName = "Pickup Sparkles";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly List<RendererMaterials> materialStates = new List<RendererMaterials>();
    private Renderer[] itemRenderers;
    private ParticleSystem sparkles;
    private Material sparkleMaterial;
    private Texture2D sparkleTexture;
    private bool targeted;
    private bool pickedUp;

    public string ItemName => itemName;

    private sealed class RendererMaterials
    {
        public Renderer Renderer;
        public Material[] Originals;
        public Material[] Glowing;
    }

    // Called when the component is first added in the Inspector.
    private void Reset()
    {
        if (GetComponentInChildren<Collider>() != null) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        Bounds localBounds = ToLocalBounds(worldBounds);
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = localBounds.center;
        box.size = localBounds.size;
    }

    private void Awake()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        List<Renderer> meshes = new List<Renderer>();
        foreach (Renderer renderer in all)
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                meshes.Add(renderer);
        itemRenderers = meshes.ToArray();
    }

    private void OnEnable()
    {
        if (itemRenderers == null) return;
        pickedUp = false;
        CreateGlowMaterials();
        CreateSparkles();
    }

    private void Update()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
        float focusBoost = targeted ? 1.6f : 1f;

        foreach (RendererMaterials state in materialStates)
        {
            for (int i = 0; i < state.Glowing.Length; i++)
            {
                Material original = state.Originals[i];
                Material glowing = state.Glowing[i];
                if (original == null || glowing == null) continue;

                int colorProperty = original.HasProperty(BaseColorId) ? BaseColorId : ColorId;
                if (original.HasProperty(colorProperty))
                {
                    Color baseColor = original.GetColor(colorProperty);
                    glowing.SetColor(colorProperty, Color.Lerp(baseColor, highlightColor, 0.35f + 0.2f * pulse));
                }

                if (glowing.HasProperty(EmissionColorId))
                    glowing.SetColor(EmissionColorId, highlightColor * glowStrength * (0.65f + pulse * 0.7f) * focusBoost);
            }
        }
    }

    public void SetTargeted(bool value)
    {
        targeted = value;
        if (sparkles != null)
        {
            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.rateOverTime = sparkleRate * (targeted ? 2f : 1f);
        }
    }

    public void PickUp()
    {
        if (pickedUp) return;
        pickedUp = true;
        onPickedUp.Invoke();
        Debug.Log("Picked up: " + itemName, this);

        if (afterPickup == PickupAction.DisableObject)
            gameObject.SetActive(false);
        else
            Destroy(gameObject);
    }

    private void CreateGlowMaterials()
    {
        foreach (Renderer renderer in itemRenderers)
        {
            Material[] originals = renderer.sharedMaterials;
            Material[] glowing = new Material[originals.Length];

            for (int i = 0; i < originals.Length; i++)
            {
                if (originals[i] == null) continue;
                glowing[i] = new Material(originals[i]);
                glowing[i].name = originals[i].name + " (Pickup Glow)";
                if (glowing[i].HasProperty(EmissionColorId))
                    glowing[i].EnableKeyword("_EMISSION");
            }

            renderer.sharedMaterials = glowing;
            materialStates.Add(new RendererMaterials { Renderer = renderer, Originals = originals, Glowing = glowing });
        }
    }

    private void CreateSparkles()
    {
        Bounds localBounds = GetLocalBounds();
        GameObject effect = new GameObject(SparkleObjectName);
        effect.SetActive(false);
        effect.transform.SetParent(transform, false);
        effect.transform.localPosition = localBounds.center;

        sparkles = effect.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = sparkles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.13f);
        main.startColor = highlightColor;
        main.maxParticles = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = sparkles.emission;
        emission.rateOverTime = sparkleRate;

        ParticleSystem.ShapeModule shape = sparkles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = localBounds.size + Vector3.one * 0.3f;

        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = sparkles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(fade);

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            sparkleTexture = CreateStarTexture();
            sparkleMaterial = new Material(shader) { name = "Pickup Sparkle (Runtime)" };
            sparkleMaterial.SetTexture("_BaseMap", sparkleTexture);
            sparkleMaterial.SetColor("_BaseColor", Color.white);
            sparkleMaterial.SetFloat("_Surface", 1f);
            sparkleMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            sparkleMaterial.SetInt("_DstBlend", (int)BlendMode.One);
            sparkleMaterial.SetInt("_ZWrite", 0);
            sparkleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sparkleMaterial.renderQueue = (int)RenderQueue.Transparent;
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkleMaterial;
        }

        effect.SetActive(true);
        sparkles.Play();
    }

    private static Texture2D CreateStarTexture()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Pickup Star (Runtime)";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - 15.5f) / 15.5f;
            float dy = (y - 15.5f) / 15.5f;
            float core = Mathf.Exp(-22f * (dx * dx + dy * dy));
            float horizontal = Mathf.Exp(-90f * dy * dy) * (1f - Mathf.Abs(dx));
            float vertical = Mathf.Exp(-90f * dx * dx) * (1f - Mathf.Abs(dy));
            float alpha = Mathf.Clamp01(core + 0.7f * Mathf.Max(horizontal, vertical));
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply();
        return texture;
    }

    private Bounds GetLocalBounds()
    {
        if (itemRenderers.Length == 0)
        {
            Collider collider = GetComponentInChildren<Collider>();
            return collider != null ? ToLocalBounds(collider.bounds) : new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds worldBounds = itemRenderers[0].bounds;
        for (int i = 1; i < itemRenderers.Length; i++)
            worldBounds.Encapsulate(itemRenderers[i].bounds);
        return ToLocalBounds(worldBounds);
    }

    private Bounds ToLocalBounds(Bounds worldBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Bounds local = new Bounds(transform.InverseTransformPoint(worldBounds.center), Vector3.zero);
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
            local.Encapsulate(transform.InverseTransformPoint(new Vector3(
                x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z)));
        return local;
    }

    private void OnDisable()
    {
        targeted = false;
        foreach (RendererMaterials state in materialStates)
        {
            if (state.Renderer != null) state.Renderer.sharedMaterials = state.Originals;
            foreach (Material material in state.Glowing)
                if (material != null) Destroy(material);
        }
        materialStates.Clear();

        if (sparkles != null) Destroy(sparkles.gameObject);
        if (sparkleMaterial != null) Destroy(sparkleMaterial);
        if (sparkleTexture != null) Destroy(sparkleTexture);
        sparkles = null;
        sparkleMaterial = null;
        sparkleTexture = null;
    }
}
