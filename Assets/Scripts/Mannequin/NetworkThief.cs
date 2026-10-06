using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Воришка в онлайне. Мозг (ThiefBrain), агент и видимость работают только на сервере.
/// Клиенты получают позицию (NetworkTransform), анимацию (NetworkMannequinAnimation) и id вещи,
/// которую он несёт, — модель в руке (шапку на голове) клиент вешает сам.
/// Вещи на уровне и в гнезде синхронизирует NetworkPickup, кражу из комплекта — NetworkPlayer.
/// </summary>
[DefaultExecutionOrder(-900)]
[RequireComponent(typeof(ThiefBrain), typeof(MannequinVisibility))]
public sealed class NetworkThief : NetworkBehaviour
{
    // Имя ClothingDefinition вещи в руках; пусто — руки пустые
    private readonly SyncVar<string> carried = new SyncVar<string>(string.Empty);

    private ThiefBrain brain;
    private MannequinVisibility visibility;
    private NavMeshAgent agent;

    private bool AsServer => NetworkObject != null && IsServerInitialized;

    private void Awake()
    {
        brain = GetComponent<ThiefBrain>();
        visibility = GetComponent<MannequinVisibility>();
        agent = GetComponent<NavMeshAgent>();
        brain.CarriedChanged += OnCarriedChanged;
        carried.OnChange += OnCarriedSynced;
    }

    private void Start()
    {
        if (agent != null)
            agent.enabled = NetworkLobby.Instance == null || NetworkLobby.Instance.Offline || FishNet.InstanceFinder.IsServerStarted;
    }

    private void OnDestroy()
    {
        if (brain != null) brain.CarriedChanged -= OnCarriedChanged;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (IsServerStarted) return;
        // Клиент: всё решает сервер
        brain.enabled = false;
        visibility.enabled = false;
        if (agent != null) agent.enabled = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsServerInitialized) brain.ShowCarried(FindClothing(carried.Value));
    }

    private void OnCarriedChanged(ClothingDefinition clothing)
    {
        if (AsServer) carried.Value = clothing != null ? clothing.name : string.Empty;
    }

    private void OnCarriedSynced(string previous, string next, bool asServer)
    {
        if (asServer || IsServerInitialized) return; // на сервере (и у хоста) модель уже в руках
        brain.ShowCarried(FindClothing(next));
    }

    /// <summary>Одежда по имени — ищем среди вещей своей комнаты (каталог не нужен).</summary>
    private ClothingDefinition FindClothing(string clothingId)
    {
        if (string.IsNullOrEmpty(clothingId)) return null;
        foreach (ClothingPickup pickup in ClothingPickup.All)
            if (pickup != null && pickup.Clothing != null && pickup.Clothing.name == clothingId &&
                pickup.gameObject.scene == gameObject.scene)
                return pickup.Clothing;
        return null;
    }
}
