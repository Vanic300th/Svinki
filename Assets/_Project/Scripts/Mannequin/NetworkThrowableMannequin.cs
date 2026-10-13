using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(ThrowableMannequin))]
public sealed class NetworkThrowableMannequin : NetworkBehaviour
{
    private readonly SyncVar<NetworkObject> carrier = new SyncVar<NetworkObject>();
    private ThrowableMannequin mannequin;
    // Offline scene activation happens before FishNet initializes this behaviour.
    public bool ServerActive => NetworkObject != null && IsServerInitialized;
    private void Awake()
    {
        mannequin = GetComponent<ThrowableMannequin>();
        carrier.OnChange += CarrierChanged;
    }
    public void SetCarrier(NetworkObject player) { if (ServerActive) carrier.Value = player; }
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsServerInitialized)
        {
            mannequin.SetClientPhysics();
            mannequin.ApplyHolder(carrier.Value != null ? carrier.Value.GetComponent<PlayerAvatar>() : null);
        }
    }
    private void CarrierChanged(NetworkObject previous, NetworkObject next, bool asServer)
    {
        if (!asServer && !IsServerInitialized) mannequin.ApplyHolder(next != null ? next.GetComponent<PlayerAvatar>() : null);
    }
}
