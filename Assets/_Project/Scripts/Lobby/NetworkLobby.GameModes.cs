using System.Linq;
using UnityEngine;

public sealed partial class NetworkLobby
{
    private SessionGameMode selectedGame;
    private bool gameSelected;
    public SessionGameMode SelectedGame => Offline || hosting ? selectedGame : Snapshot.gameMode;
    public bool GameSelected => Offline || hosting ? gameSelected : Snapshot.gameSelected;
    public string SelectedGameScene => SessionGameModes.SceneName(SelectedGame);
    public bool InGrandpaPrototype => Offline
        ? PlayerRegistry.Players.Any(p => p != null && p.IsLocal && p.gameObject.scene.name == SessionGameModes.GrandpaScene)
        : Snapshot.phase == SessionPhase.Round && SelectedGame == SessionGameMode.GrandpasFridge;

    // Called through the existing owned-player RPC after the panel's proximity validation.
    public bool SelectLiftGame(PlayerAvatar player, SessionGameMode mode)
    {
        if (!SessionGameModes.IsValid(mode) || player == null || !player.IsAlive ||
            player.gameObject.scene.name != FriendslopSceneName || Busy || offlineChangingScene) return false;
        if (!Offline && (!hosting || phase != SessionPhase.Lobby ||
            player.GetComponent<NetworkPlayer>()?.ParticipantId != hostIdentity)) return false;
        selectedGame = mode; gameSelected = true;
        var lift = Offline
            ? player.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LobbyFreightElevator>(true)).FirstOrDefault()
            : StartLift;
        lift?.Step(0, 0, false);
        if (Offline) Changed?.Invoke();
        else Publish();
        return true;
    }

    public void ReturnFromGrandpaPrototype()
    {
        if (!InGrandpaPrototype || Busy) return;
        if (Offline) { gameSelected = false; offlineRound++; LoadOfflineWorld(FriendslopSceneName); }
        else Send(SessionAction.ReturnToLobby);
    }
}
