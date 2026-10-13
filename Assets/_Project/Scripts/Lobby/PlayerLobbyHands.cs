using UnityEngine;

public sealed class PlayerLobbyHands : MonoBehaviour
{
    public LobbyProp Held { get; private set; }
    public void Set(LobbyProp prop) => Held = prop;
    public void Release(bool thrown)
    {
        if (Held == null) return;
        if (Held.Authority) Held.Release(thrown);
        else GetComponent<NetworkPlayer>()?.RequestLobbyRelease(thrown);
    }
    private void OnDisable() { if (Held != null && Held.Authority) Held.Release(false); Held = null; }
}
