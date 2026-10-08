using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
[RequireComponent(typeof(MonkeyToy))]
public sealed class NetworkMonkeyToy : NetworkBehaviour
{
    private readonly SyncVar<NetworkObject> carrier=new SyncVar<NetworkObject>();
    private readonly SyncVar<uint> swing=new SyncVar<uint>();
    private MonkeyToy toy;private float nextSend;
    private void Awake(){toy=GetComponent<MonkeyToy>();carrier.OnChange+=(old,next,asServer)=>{if(!asServer&&!IsServerInitialized)toy.ApplyHolder(next!=null?next.GetComponent<PlayerAvatar>():null);};swing.OnChange+=(old,next,asServer)=>{if(!asServer&&!IsServerInitialized)toy.AnimateSwing();};}
    public override void OnStartServer(){base.OnStartServer();toy.BeginPhysics();}
    public override void OnStartClient(){base.OnStartClient();if(!IsServerInitialized){toy.BeginPhysics();toy.ApplyHolder(carrier.Value!=null?carrier.Value.GetComponent<PlayerAvatar>():null);}}
    public void SetCarrier(PlayerAvatar avatar){if(IsServerInitialized)carrier.Value=avatar!=null?avatar.GetComponent<NetworkObject>():null;}
    public void SetSwing(){if(IsServerInitialized)swing.Value++;}
    private void Update(){if(IsServerInitialized&&Time.unscaledTime>=nextSend){nextSend=Time.unscaledTime+.05f;PoseObserversRpc(toy.Capture());}}
    [ObserversRpc]private void PoseObserversRpc(RagdollFrame frame,Channel channel=Channel.Unreliable){if(!IsServerInitialized)toy.Receive(frame);}
}
