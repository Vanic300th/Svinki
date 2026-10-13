using FishNet.Object;
using UnityEngine;

public enum LobbyAction : byte { Toggle, AssignPhoto, ClearPhoto, PlayPause, Load, Seek, Stop, Skip, Reset, ClearBoard, Stroke, WearMemory }

/// <summary>Routes new social objects through the existing player's owned FishNet RPC.</summary>
public abstract class LobbyInteractable : NetworkBehaviour
{
    public bool Authority => NetworkLobby.Instance?.Offline == true || NetworkObject != null && IsServerInitialized;
    public abstract string Prompt(PlayerAvatar player);
    public virtual void Interact(PlayerAvatar player) => Request(player, LobbyAction.Toggle);
    public void Request(PlayerAvatar player, LobbyAction action, string text = "", int index = 0, Vector2[] points = null)
    {
        if (player == null) return;
        if (NetworkLobby.Instance?.Offline == true)
        {
            if (Validate(player)) Apply(player, action, text, index, points);
            return;
        }
        var network = player.GetComponent<NetworkPlayer>();
        if (network != null) network.RequestLobbyAction(NetworkObject, action, text, index, points);
        else if (Validate(player)) Apply(player, action, text, index, points);
    }
    public bool Validate(PlayerAvatar player)
    {
        if (!Authority || player == null || !player.IsAlive || player.gameObject.scene != gameObject.scene ||
            player.GetComponent<PlayerKnockdown>()?.IsDown == true) return false;
        var network = player.GetComponent<NetworkPlayer>();
        if (NetworkLobby.Instance?.Offline != true && network != null && NetworkLobby.Instance?.CanUseLobby(network) != true) return false;
        var collider = GetComponent<Collider>();
        Vector3 point = collider != null ? collider.ClosestPoint(player.EyePosition) : transform.position;
        var direction = point - player.EyePosition;
        if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z) || direction.sqrMagnitude > 4 * 4) return false;
        // Distance alone would allow controlling the TV or somebody's locker through a wall.
        return direction.sqrMagnitude < .01f || !gameObject.scene.GetPhysicsScene().Raycast(player.EyePosition, direction.normalized,
            out var hit, direction.magnitude + .05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.collider.transform.IsChildOf(transform);
    }
    public abstract void Apply(PlayerAvatar player, LobbyAction action, string text, int index, Vector2[] points);
    public virtual void LobbyTick(float delta) { }
    public static string Identity(PlayerAvatar player) => NetworkLobby.Instance?.Offline == true ? "offline" : player?.GetComponent<NetworkPlayer>()?.ParticipantId ?? "offline";
    public static string Nickname(PlayerAvatar player) => NetworkLobby.Instance?.Offline == true ? NetworkLobby.Instance.Nickname : player?.GetComponent<NetworkPlayer>()?.ParticipantName ?? NetworkLobby.Instance?.Nickname ?? "Pig";
}
