using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Сетевая вещь. Сервер решает, лежит ли она и где; клиенты только показывают.
/// Подбор не удаляет объект, а прячет его (PickupItem.SetAvailable) — так Воришка может
/// вернуть вещь на уровень (в гнездо или под ноги), и это увидят все в комнате.
/// </summary>
[RequireComponent(typeof(PickupItem))]
public sealed class NetworkPickup : NetworkBehaviour
{
    // Сначала место, потом «видна», чтобы вещь не мелькнула на старом месте
    private readonly SyncVar<Vector3> position = new SyncVar<Vector3>();
    private readonly SyncVar<bool> available = new SyncVar<bool>(true);

    private PickupItem item;

    private void Awake()
    {
        item = GetComponent<PickupItem>();
        position.OnChange += OnPositionChanged;
        available.OnChange += OnAvailableChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        position.Value = transform.position;
        available.Value = item.IsAvailable;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (IsServerStarted) return; // хост: состояние и так общее
        transform.position = position.Value;
        item.SetAvailable(available.Value);
    }

    /// <summary>Сервер: игрок подобрал вещь.</summary>
    public void TryCollect(NetworkPlayer player)
    {
        if (!IsServerStarted || player == null || !item.IsAvailable) return;
        ClothingPickup clothing = GetComponent<ClothingPickup>();
        if (clothing != null && !player.AwardClothing(clothing.Clothing)) return;
        item.SetAvailable(false); // спрятать у всех: Update ниже разошлёт
    }

    private void Update()
    {
        // Сервер раздаёт то, что сделали с вещью (подобрали, Воришка унёс или положил в гнездо)
        if (NetworkObject == null || !IsServerStarted) return;
        if (position.Value != transform.position) position.Value = transform.position;
        if (available.Value != item.IsAvailable) available.Value = item.IsAvailable;
    }

    private void OnPositionChanged(Vector3 previous, Vector3 next, bool asServer)
    {
        if (asServer || IsServerStarted) return;
        transform.position = next;
    }

    private void OnAvailableChanged(bool previous, bool next, bool asServer)
    {
        if (asServer || IsServerStarted) return;
        item.SetAvailable(next);
    }
}
