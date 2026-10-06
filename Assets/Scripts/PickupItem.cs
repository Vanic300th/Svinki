using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PickupItem : MonoBehaviour
{
    /// <summary>
    /// HideObject — предмет прячется (выключаются модель и коллайдеры), но объект остаётся в сцене:
    /// его можно вернуть на уровень (Воришка), и в онлайне сетевой объект не пропадает.
    /// </summary>
    public enum PickupAction { HideObject, DestroyObject }

    [Header("Pickup")]
    [SerializeField] private string itemName = "Предмет";
    [SerializeField] private PickupAction afterPickup = PickupAction.HideObject;
    [SerializeField] private UnityEvent onPickedUp = new UnityEvent();

    // Kept for compatibility with existing prefab setup tools. Pickups use normal lit materials.
    [SerializeField, HideInInspector] private float glowStrength;
    [SerializeField, HideInInspector] private float sparkleRate;
    [SerializeField, HideInInspector] private bool preserveBaseColor;
    private readonly List<RendererMaterials> materialStates = new List<RendererMaterials>();
    private Renderer[] itemRenderers;
    private bool pickedUp, targeted;
    private bool available = true;
    private bool[] rendererShown;
    private Collider[] itemColliders;
    private bool[] colliderShown;

    public string ItemName => itemName;
    /// <summary>Кто подобрал предмет последним (объект камеры или игрока). null — неизвестно.</summary>
    public GameObject Picker { get; private set; }
    /// <summary>Предмет лежит на уровне: виден и его можно подобрать.</summary>
    public bool IsAvailable => available;
    public event Action<PickupItem> PickedUp;
    /// <summary>Предмет спрятали или снова показали.</summary>
    public event Action<PickupItem> AvailabilityChanged;

    private sealed class RendererMaterials
    {
        public Renderer Renderer;
        public Material[] Originals, Plain, Highlight;
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
        // Триггер: луч подбора его находит, а взгляд манекенов и игрока сквозь него проходит
        box.isTrigger = true;
    }

    private void Awake()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        List<Renderer> meshes = new List<Renderer>();
        foreach (Renderer renderer in all)
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                meshes.Add(renderer);
        itemRenderers = meshes.ToArray();

        // Запоминаем, что было включено изначально: часть рендеров (старый кубик) выключена специально
        rendererShown = new bool[itemRenderers.Length];
        for (int i = 0; i < itemRenderers.Length; i++) rendererShown[i] = itemRenderers[i].enabled;
        itemColliders = GetComponentsInChildren<Collider>(true);
        colliderShown = new bool[itemColliders.Length];
        for (int i = 0; i < itemColliders.Length; i++) colliderShown[i] = itemColliders[i].enabled;
    }

    private void OnEnable()
    {
        if (itemRenderers == null) return;
        if (available) pickedUp = false;
        foreach (Renderer renderer in itemRenderers)
        {
            Material[] originals = renderer.sharedMaterials;
            Material[] lit = new Material[originals.Length];
            for (int i = 0; i < lit.Length; i++) lit[i] = ClothingVisuals.Plain(originals[i]);
            Material[] highlight = new Material[originals.Length];
            for (int i = 0; i < highlight.Length; i++) highlight[i] = ClothingVisuals.Highlight(originals[i]);
            materialStates.Add(new RendererMaterials { Renderer = renderer, Originals = originals, Plain = lit, Highlight = highlight });
            renderer.sharedMaterials = lit;
        }
        foreach (Light light in GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (ParticleSystem effect in GetComponentsInChildren<ParticleSystem>(true))
        {
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.GetComponent<ParticleSystemRenderer>().enabled = false;
        }
        ApplyAvailability();
    }

    public bool IsTargeted => targeted;
    public void SetTargeted(bool value)
    {
        value = value && available && isActiveAndEnabled && GetComponent<ClothingPickup>() != null;
        if (targeted == value) return;
        targeted = value;
        foreach (RendererMaterials state in materialStates)
            if (state.Renderer != null) state.Renderer.sharedMaterials = value ? state.Highlight : state.Plain;
    }

    public void PickUp() => PickUp(null);

    /// <summary>Подобрать. picker — кто подобрал (игрок или его камера), чтобы вещь ушла именно ему.</summary>
    public void PickUp(GameObject picker)
    {
        if (pickedUp || !available) return;
        ClothingPickup clothing = GetComponent<ClothingPickup>();
        if (clothing != null && !clothing.CanCollect(picker)) return;
        pickedUp = true;
        Picker = picker;
        PickedUp?.Invoke(this);
        onPickedUp.Invoke();
        Debug.Log("Picked up: " + itemName, this);

        if (afterPickup == PickupAction.HideObject)
            SetAvailable(false);
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// Спрятать (false) или снова выложить (true) предмет, не выключая объект.
    /// Спрятанный не виден, не светится и луч подбора его не находит.
    /// </summary>
    public void SetAvailable(bool value)
    {
        if (available == value) return;
        available = value;
        if (available) pickedUp = false;
        else SetTargeted(false);
        ApplyAvailability();
        AvailabilityChanged?.Invoke(this);
    }

    private void ApplyAvailability()
    {
        if (itemRenderers != null)
            for (int i = 0; i < itemRenderers.Length; i++)
                if (itemRenderers[i] != null) itemRenderers[i].enabled = available && rendererShown[i];
        if (itemColliders != null)
            for (int i = 0; i < itemColliders.Length; i++)
                if (itemColliders[i] != null) itemColliders[i].enabled = available && colliderShown[i];
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
            if (state.Renderer != null) state.Renderer.sharedMaterials = state.Originals;
        materialStates.Clear();
    }
}
