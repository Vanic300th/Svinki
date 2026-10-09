using UnityEngine;
[DisallowMultipleComponent]
public sealed class PlayerMonkeyCarry : MonoBehaviour
{
    public MonkeyToy Held { get; private set; }
    public void Set(MonkeyToy toy)=>Held=toy;
    public void Clear(MonkeyToy toy){if(Held==toy)Held=null;}
    public void Swing(){if(Held==null)return;if(Held.HasAuthority)Held.Swing();else GetComponent<NetworkPlayer>()?.RequestMonkeyAction(true);}
    public void Release(){if(Held==null)return;if(Held.HasAuthority)Held.Release();else GetComponent<NetworkPlayer>()?.RequestMonkeyAction(false);}
    private void OnDisable(){if(Held!=null&&Held.HasAuthority)Held.Release();Held=null;}
}
