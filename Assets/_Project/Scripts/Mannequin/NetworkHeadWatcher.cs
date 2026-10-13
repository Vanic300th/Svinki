using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Подглядывающий в онлайне. Сервер решает, видят ли его игроки комнаты и куда повернуть голову
/// (MannequinHeadWatcher работает там как обычно), а клиентам уходят только два угла головы и сигнал скрипа.
/// </summary>
[RequireComponent(typeof(MannequinHeadWatcher))]
public sealed class NetworkHeadWatcher : NetworkBehaviour
{
    private readonly SyncVar<Vector2> angles = new SyncVar<Vector2>(new SyncTypeSettings(0.05f));

    private MannequinHeadWatcher watcher;
    private MannequinVisibility visibility;

    private bool AsServer => NetworkObject != null && IsServerInitialized;
    private bool AsClientOnly => NetworkObject != null && IsClientInitialized && !IsServerInitialized;

    private void Awake()
    {
        watcher = GetComponent<MannequinHeadWatcher>();
        visibility = GetComponent<MannequinVisibility>();
        watcher.Creaked += OnCreaked;
    }

    private void OnDestroy()
    {
        if (watcher != null) watcher.Creaked -= OnCreaked;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (IsServerStarted) return;
        // Клиент: голову поворачивает сервер, видимость считать незачем
        watcher.SetRemote(true);
        if (visibility != null) visibility.enabled = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsServerInitialized) watcher.SetRemoteAngles(angles.Value);
    }

    private void Update()
    {
        if (AsServer)
        {
            Vector2 current = watcher.Angles;
            if ((current - angles.Value).sqrMagnitude > 0.04f) angles.Value = current;
        }
        else if (AsClientOnly)
        {
            watcher.SetRemoteAngles(angles.Value);
        }
    }

    private void OnCreaked()
    {
        if (AsServer) CreakObserversRpc();
    }

    [ObserversRpc(ExcludeServer = true)]
    private void CreakObserversRpc()
    {
        watcher.PlayCreakRemote();
    }
}
