using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerMannequinCarry : MonoBehaviour
{
    public ThrowableMannequin Held { get; private set; }
    public void Set(ThrowableMannequin mannequin) => Held = mannequin;
    public void Clear(ThrowableMannequin mannequin) { if (Held == mannequin) Held = null; }
    public void Release(bool thrown)
    {
        if (Held == null) return;
        var player = GetComponent<NetworkPlayer>();
        if (Held.HasAuthority) Held.Release(thrown);
        else if (player != null && player.IsOwner) player.RequestMannequinRelease(thrown);
    }
    private void OnDisable()
    {
        if (Held != null && Held.HasAuthority) Held.Release(false);
        Held = null;
    }
}
