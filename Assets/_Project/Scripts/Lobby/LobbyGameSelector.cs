using UnityEngine;

/// <summary>The host chooses the whole group's destination at the freight-lift panel.</summary>
public sealed class LobbyGameSelector : LobbyInteractable
{
    public override string Prompt(PlayerAvatar player) => GameLanguage.Code == "ru" ? "E — выбрать игру" : "E — choose game";
    public override void Interact(PlayerAvatar player) => LobbyPanel.Open(this, player);
    public override void Apply(PlayerAvatar player, LobbyAction action, string text, int index, Vector2[] points)
    {
        if (action == LobbyAction.SelectGame && index >= 0 && index <= (int)SessionGameMode.GrandpasFridge)
            NetworkLobby.Instance?.SelectLiftGame(player, (SessionGameMode)index);
    }
}
