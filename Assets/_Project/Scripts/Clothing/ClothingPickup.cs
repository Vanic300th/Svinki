using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Вещь одежды на уровне. Когда игрок её подбирает, она попадает в его PlayerOutfit
/// (а HUD-манекен показывает её сам). Объект при этом только прячется, а не удаляется:
/// Воришка может вернуть его на уровень — в своё гнездо или под ноги.
/// В онлайне подбор решает сервер (NetworkPickup), а этот скрипт только хранит, что это за вещь.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(PickupItem))]
public sealed class ClothingPickup : MonoBehaviour
{
    private static readonly List<ClothingPickup> all = new List<ClothingPickup>();
    private static readonly RaycastHit[] floorHits = new RaycastHit[16];

    [SerializeField] private ClothingDefinition clothing;
    [Tooltip("Запасной вариант для сцен без PlayerOutfit: одежда сразу показывается на этом HUD-манекене")]
    [SerializeField] private MannequinWardrobe wardrobe;

    private float hoverHeight = -1f;
    private bool carriedByThief, consumedByCargo;
    private float protectedUntil;
    public bool CanBeStolen => !carriedByThief && !consumedByCargo && Time.time >= protectedUntil;
    public void ClaimByThief() => carriedByThief = true;
    public void ProtectFromThieves(float seconds) => protectedUntil = Time.time + seconds;

    /// <summary>Все вещи одежды во всех сценах (комнатах), и лежащие, и подобранные.</summary>
    public static IReadOnlyList<ClothingPickup> All => all;

    public ClothingDefinition Clothing => clothing;
    public PickupItem Item { get; private set; }
    /// <summary>Вещь лежит на уровне и её можно взять.</summary>
    public bool IsOnFloor => Item != null && Item.IsAvailable && gameObject.activeInHierarchy;
    /// <summary>На какой высоте над полом вещь висела в сцене. Так же она будет висеть, если её перенесут.</summary>
    public float HoverHeight
    {
        get
        {
            if (hoverHeight < 0f) hoverHeight = MeasureHoverHeight();
            return hoverHeight;
        }
    }

    private void Reset()
    {
        wardrobe = FindAnyObjectByType<MannequinWardrobe>();
    }

    private void Awake()
    {
        Item = GetComponent<PickupItem>();
        if (!all.Contains(this)) all.Add(this);
    }

    // Меряем в Start: к этому моменту все коллайдеры сцены уже на месте
    private void Start()
    {
        if (hoverHeight < 0f) hoverHeight = MeasureHoverHeight();
    }

    private void OnDestroy() => all.Remove(this);

    private void OnEnable()
    {
        GetComponent<PickupItem>().PickedUp += HandlePickup;
    }

    private void OnDisable()
    {
        PickupItem pickup = GetComponent<PickupItem>();
        if (pickup != null) pickup.PickedUp -= HandlePickup;
    }

    /// <summary>Спрятать вещь с уровня (её унёс Воришка). В комплект игроку она не попадает.</summary>
    public void TakeForCargo() { consumedByCargo = true; TakeAway(); }

    public void TakeAway()
    {
        if (Item != null) Item.SetAvailable(false);
    }

    /// <summary>Положить вещь на уровень: точка на полу, вещь повиснет над ней на своей высоте.</summary>
    public void PlaceAt(Vector3 floorPoint)
    {
        carriedByThief = false;
        float height = HoverHeight;
        transform.position = floorPoint + Vector3.up * height;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (Item != null) Item.SetAvailable(true);
    }

    /// <summary>Подобранная (спрятанная) вещь с этой одеждой в этой комнате — её объект Воришка возвращает на уровень.</summary>
    public static ClothingPickup FindPickedUp(ClothingDefinition clothing, Scene scene)
    {
        foreach (ClothingPickup pickup in all)
            if (pickup != null && pickup.clothing == clothing && pickup.gameObject.scene == scene &&
                pickup.Item != null && !pickup.Item.IsAvailable && !pickup.carriedByThief && !pickup.consumedByCargo)
                return pickup;
        return null;
    }

    public bool CanCollect(GameObject picker)
    {
        if (clothing == null) return false;
        PlayerAvatar player = PlayerRegistry.FromObject(picker);
        if (player == null && PlayerRegistry.Players.Count == 1) player = PlayerRegistry.Players[0];
        return player != null && player.IsAlive && player.Outfit != null && player.Outfit.CanEquip(clothing);
    }

    private void HandlePickup(PickupItem item)
    {
        // В онлайне вещь выдаёт сервер через NetworkPlayer
        NetworkPickup networkPickup = GetComponent<NetworkPickup>();
        if (networkPickup != null && networkPickup.isActiveAndEnabled) return;
        if (clothing == null)
        {
            Debug.LogWarning("Для предмета не назначена одежда.", this);
            return;
        }

        // Кто подобрал — тому и в комплект. Если игрок один, то ему.
        PlayerAvatar player = PlayerRegistry.FromObject(item.Picker);
        if (player == null && PlayerRegistry.Players.Count == 1) player = PlayerRegistry.Players[0];
        if (player != null && player.Outfit != null)
        {
            player.Outfit.Add(clothing);
            return;
        }

        // Старый путь: сцена без PlayerOutfit — сразу на HUD-манекен
        if (wardrobe == null) wardrobe = FindAnyObjectByType<MannequinWardrobe>();
        if (wardrobe != null) wardrobe.Equip(clothing);
        else Debug.LogWarning("Манекен для отображения одежды не найден.", this);
    }

    private float MeasureHoverHeight()
    {
        // Физика своей сцены: в онлайне у каждой комнаты на сервере она отдельная
        PhysicsScene physics = gameObject.scene.GetPhysicsScene();
        int count = physics.Raycast(transform.position, Vector3.down, floorHits, 10f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (floorHits[i].collider.transform.IsChildOf(transform)) continue;
            best = Mathf.Min(best, floorHits[i].distance);
        }
        return float.IsPositiveInfinity(best) ? 1f : best;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();
}
