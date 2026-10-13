using System;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>Playable social scene using the existing session, ownership and scene pipeline.</summary>
public sealed partial class NetworkLobby
{
    public const string FriendslopSceneName = "FriendslopLobby";
    private int offlineRound = 1;
    public int LobbyRound => Offline ? offlineRound : Snapshot.round;
    public bool CanCommunicate(NetworkPlayer player) => CanEditOutfit(player) || CanUseLobby(player);
    private bool offlineChangingScene;
    private float nextStartLiftCheck;
    private LobbyFreightElevator StartLift => roundScene.IsValid() && roundScene.isLoaded && roundScene.name == FriendslopSceneName
        ? roundScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LobbyFreightElevator>(true)).FirstOrDefault() : null;
    private void LoadFriendslop(params NetworkConnection[] clients)
    {
        if (clients.Length == 0) return;
        var data = new SceneLoadData(FriendslopSceneName);
        data.Options.AllowStacking = false;
        manager.SceneManager.LoadConnectionScenes(clients, data);
    }
    private void DespawnSessionAvatars()
    {
        foreach (var member in members.Values)
        {
            if (member.Avatar != null && member.Avatar.IsSpawned) manager.ServerManager.Despawn(member.Avatar);
            member.Avatar = null;
        }
    }
    public bool CanUseLobby(NetworkPlayer player) => hosting && phase == SessionPhase.Lobby && player != null &&
        player.gameObject.scene == roundScene && roundScene.name == FriendslopSceneName &&
        members.TryGetValue(player.ParticipantId, out var member) && member.Avatar == player.NetworkObject &&
        member.Connection != null && !member.Spectator && !member.Eliminated && !player.IsDead;
    private bool InStartLift(Member member, LobbyFreightElevator lift) => member.Connection != null && member.Avatar != null &&
        member.Avatar.gameObject.scene == roundScene && member.Avatar.GetComponent<PlayerAvatar>().IsAlive &&
        member.Avatar.GetComponent<PlayerKnockdown>()?.IsDown != true && lift.Contains(member.Avatar.transform.position);
    private bool AllInStartLift()
    {
        var lift = StartLift;
        return lift != null && members.Count > 0 && members.Values.All(member => InStartLift(member, lift)) &&
            lift.CountdownFinished;
    }
    private void TickStartLift()
    {
        if ((!hosting && !Offline) || Busy || offlineChangingScene || Results != null || Time.unscaledTime < nextStartLiftCheck) return;
        nextStartLiftCheck = Time.unscaledTime + .2f;
        if (Offline)
        {
            var player = PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal && p.gameObject.scene.name == FriendslopSceneName);
            if (player == null) return;
            var lift = player.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LobbyFreightElevator>(true)).FirstOrDefault();
            if (lift != null && lift.Step(player.IsAlive && player.GetComponent<PlayerKnockdown>()?.IsDown != true && lift.Contains(player.Position) ? 1 : 0, 1, true))
                LoadOfflineWorld("SampleScene");
            return;
        }
        if (phase != SessionPhase.Lobby || StartLift == null) return;
        var start = StartLift;
        bool changed = false;
        foreach (var member in members.Values)
        {
            bool inside = InStartLift(member, start);
            if (member.Ready != inside) { member.Ready = inside; changed = true; }
        }
        if (changed) Publish();
        if (start.Step(members.Values.Count(p => p.Ready), members.Count, members.Values.All(p => p.Connection != null && p.Avatar != null))) BeginRound();
    }
    private async void LoadOfflineWorld(string sceneName)
    {
        int token = ++operation;
        Busy = true; offlineChangingScene = true; offlineResults = null;
        InventoryOpen = false; CargoOpen = false;
        Report("Loading " + (sceneName == FriendslopSceneName ? "Friendslop lobby" : "factory run") + "…");
        try
        {
            var previous = PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal)?.gameObject.scene ?? default;
            if (previous.IsValid() && previous.isLoaded) await UnloadOffline(previous);
            var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!load.isDone) await System.Threading.Tasks.Task.Yield();
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (token != operation) { if (scene.IsValid() && scene.isLoaded) await UnloadOffline(scene); return; }
            SceneManager.SetActiveScene(scene);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<NetworkBehaviour>(true))
                    component.enabled = component is LobbyInteractable || component is LobbyFreightElevator || component is LobbyAnomalyManager;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<NetworkObject>(true))
                { component.SetIsNetworked(false); component.gameObject.SetActive(true); }
            if (sceneName == "SampleScene")
            {
                PhotoAlbum.Instance?.ResetRound();
                RoundClothingLayout.Begin(scene); RoundWorldLayout.Begin(scene);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var agent in root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) agent.enabled = true;
            }
            var player = Instantiate(offlinePlayerPrefab, new Vector3(0, .05f, sceneName == FriendslopSceneName ? -22 : -3), Quaternion.identity);
            player.GetComponentInChildren<PigAppearance>(true)?.Apply(SelectedAppearance);
            SceneManager.MoveGameObjectToScene(player, scene);
            player.GetComponent<WorldOutfitRenderer>()?.SetFirstPersonHidden(true);
            Camera.main.GetComponent<GrayboxFirstPersonCamera>().SetTarget(player.transform);
            FindAnyObjectByType<MannequinWardrobe>()?.Bind(player.GetComponent<PlayerOutfit>());
            if (sceneName == "SampleScene") RoundMonkeySpawner.Begin(scene, 1);
            MenuVisible = false;
            if (sceneName == FriendslopSceneName) RestoreOfflineLockerPhotos();
            Report(sceneName == FriendslopSceneName ? "Friendslop lobby. Enter the freight lift to begin a run." : "Factory run. Esc — menu.");
        }
        catch (Exception error) { await StopSession(); Report(error.Message); }
        finally { Busy = false; offlineChangingScene = false; Changed?.Invoke(); }
    }
}
