using System.Linq;
using TMPro;
using UnityEngine;

public sealed partial class LobbyPanel
{
    private UnityEngine.UI.Button modelGameButton, grandpaGameButton;
    private TMP_Text gameSelectionHelp;

    private void BuildGameSelection(LobbyGameSelector selector)
    {
        bool ru = GameLanguage.Code == "ru";
        content.sizeDelta = new Vector2(1080, 850);
        var surface = content.GetComponent<UnityEngine.UI.Image>();
        var style = ProjectSvinSkin.Bind(surface, ProjectSvinRole.Panel);
        style.useThemeColor = false;
        Color background = ProjectSvinTheme.Current != null ? ProjectSvinTheme.Current.panel : new Color(.09f, .12f, .12f);
        background.a = 1; surface.color = background;
        status.rectTransform.anchoredPosition = new Vector2(0, 265);
        status.rectTransform.sizeDelta = new Vector2(960, 100);
        modelGameButton = GameChoice("01 / Become a Model", 115, SessionGameMode.BecomeAModel, selector);
        Label(ru ? "Собирайте одежду и станьте звездой подиума." : "Find your outfit. Own the runway.",
            new Vector2(920, 55), new Vector2(0, 40), 25);
        grandpaGameButton = GameChoice("02 / Grandpa's Fridge", -85, SessionGameMode.GrandpasFridge, selector);
        Label(ru ? "Объешьте деда и не попадитесь!" : "Raid Grandpa's food. Don't get caught!",
            new Vector2(920, 55), new Vector2(0, -155), 25);
        gameSelectionHelp = Label("", new Vector2(950, 100), new Vector2(0, -265), 22);
        RefreshGameSelection();
    }

    private UnityEngine.UI.Button GameChoice(string title, float y, SessionGameMode mode, LobbyGameSelector selector)
    {
        var button = Button(title, new Vector2(0, y), () =>
        {
            selector.Request(player, LobbyAction.SelectGame, index: (int)mode);
            Close();
        });
        button.GetComponent<RectTransform>().sizeDelta = new Vector2(880, 86);
        var label = button.GetComponentInChildren<TMP_Text>();
        label.rectTransform.sizeDelta = new Vector2(850, 76); label.fontSize = 32;
        return button;
    }

    private void RefreshGameSelection()
    {
        var lobby = NetworkLobby.Instance;
        if (lobby == null) return;
        bool ru = GameLanguage.Code == "ru";
        bool canChoose = lobby.Offline || lobby.IsHost;
        modelGameButton.interactable = canChoose;
        grandpaGameButton.interactable = canChoose;
        modelGameButton.targetGraphic.color = ProjectSvinSkin.Selection(lobby.GameSelected && lobby.SelectedGame == SessionGameMode.BecomeAModel);
        grandpaGameButton.targetGraphic.color = ProjectSvinSkin.Selection(lobby.GameSelected && lobby.SelectedGame == SessionGameMode.GrandpasFridge);
        GameLanguage.SetLiteral(status, lobby.GameSelected
            ? (ru ? "ВЫБРАНО: " : "SELECTED: ") + SessionGameModes.Title(lobby.SelectedGame)
            : ru ? "ВЫБЕРИТЕ ИГРУ ДЛЯ ВАШЕЙ ГРУППЫ" : "CHOOSE YOUR GROUP'S GAME");
        GameLanguage.SetLiteral(gameSelectionHelp, (canChoose
            ? ru ? "Выберите игру, затем соберитесь в лифте." : "Choose a game, then gather everyone in the lift."
            : ru ? "Игру выбирает хост. Выбор общий для всей группы." : "The host chooses the game for the whole group.") + "\n" +
            (!lobby.Offline && lobby.Snapshot.players.Count(p => p.connected) > 1
                ? ru ? "Роли выбираются после загрузки дома." : "Choose roles after the house loads."
                : ru ? "В одиночку ты начнёшь за свинью." : "When playing alone, you start as a pig."));
    }
}
