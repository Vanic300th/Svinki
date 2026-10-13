using FishNet.Object;
using UnityEngine;

public sealed partial class NetworkPlayer
{
    private float nextLobbyCommand;
    public void RequestLobbyAction(NetworkObject target, LobbyAction action, string text, int index, Vector2[] points)
    {
        if (IsOwner && target != null) LobbyActionServerRpc(target, action, text, index, points);
    }
    [ServerRpc(RequireOwnership = true)]
    private void LobbyActionServerRpc(NetworkObject target, LobbyAction action, string text, int index, Vector2[] points)
    {
        if (Time.unscaledTime < nextLobbyCommand || target == null || target.gameObject.scene != gameObject.scene ||
            text == null || text.Length > 2048 || points != null && points.Length > 32) return;
        nextLobbyCommand = Time.unscaledTime + .08f;
        var item = target.GetComponent<LobbyInteractable>();
        var avatar = GetComponent<PlayerAvatar>();
        if (item != null && item.Validate(avatar)) item.Apply(avatar, action, text, index, points);
    }
    public void RequestLobbyRelease(bool thrown) { if (IsOwner) LobbyReleaseServerRpc(thrown); }
    [ServerRpc(RequireOwnership = true)]
    private void LobbyReleaseServerRpc(bool thrown) => GetComponent<PlayerLobbyHands>()?.Held?.Release(thrown);
}
