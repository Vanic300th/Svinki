using UnityEngine;

/// <summary>Shared colours for the host-assigned session slots.</summary>
public static class PlayerIdentityColor
{
    private static readonly Color[] palette =
    {
        new Color(1f, .45f, .32f),
        new Color(.30f, .95f, 1f),
        new Color(.60f, 1f, .32f),
        new Color(.78f, .55f, 1f),
        new Color(1f, .85f, .30f),
        new Color(1f, .45f, .80f)
    };

    public static Color For(NetworkPlayer player)
    {
        var participants = NetworkLobby.Instance?.Snapshot.players;
        if (player != null && participants != null)
            foreach (var participant in participants)
                if (participant.id == player.ParticipantId)
                    return palette[Mathf.Clamp(participant.colorSlot, 0, palette.Length - 1)];
        return palette[0];
    }
}
