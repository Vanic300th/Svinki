using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
[RequireComponent(typeof(MannequinStun))]
public sealed class NetworkMannequinStun : NetworkBehaviour
{
    private readonly SyncVar<MannequinStun.StunState> state=new SyncVar<MannequinStun.StunState>();
    private MannequinStun stun;private float nextSend;
    public bool ServerActive=>NetworkObject!=null&&IsServerInitialized;
    private void Awake(){stun=GetComponent<MannequinStun>();state.OnChange+=(old,next,asServer)=>{if(!asServer&&!ServerActive)stun.Apply(next);};}
    public override void OnStartClient(){base.OnStartClient();if(!ServerActive)stun.Apply(state.Value);}
    public void SetState(MannequinStun.StunState value){if(ServerActive)state.Value=value;}
    private void Update(){if(ServerActive&&stun.IsStunned&&Time.unscaledTime>=nextSend){nextSend=Time.unscaledTime+.05f;PoseObserversRpc(stun.Capture());}}
    [ObserversRpc]private void PoseObserversRpc(RagdollFrame frame,Channel channel=Channel.Unreliable){if(!ServerActive)stun.Receive(frame);}
}
