using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GrandpaRole : byte { None, Pig, Hunter }

public sealed partial class NetworkLobby
{
    private const double GrandpaChoiceSeconds = 30;
    private const double GrandpaLoadTimeout = 20;
    private const double GrandpaClassSeconds = 15;
    private double grandpaSelectionDeadline;
    private double grandpaLoadDeadline;
    private bool grandpaSoloChoice;
    private Scene offlineGrandpaClassScene;

    private const string DefaultHunterClassId = "grandpa-standard";
    private static bool IsGrandpaSelection(SessionPhase state) =>
        state == SessionPhase.ChoosingGrandpaRoles || state == SessionPhase.ChoosingGrandpaClasses;
    private static bool HouseLoaded => SceneManager.GetSceneByName(SessionGameModes.GrandpaScene).isLoaded;

    public bool ChoosingGrandpaRole => !Offline && Snapshot.phase == SessionPhase.ChoosingGrandpaRoles && HouseLoaded;
    public bool ChoosingGrandpaClass => Offline ? offlineGrandpaClassScene.isLoaded :
        Snapshot.phase == SessionPhase.ChoosingGrandpaClasses && HouseLoaded;
    public bool ChoosingSoloGrandpaClass => ChoosingGrandpaClass && (Offline || Snapshot.grandpaSoloChoice);
    public GrandpaRole AssignedGrandpaRole => Offline ? GrandpaRole.Pig : LocalGrandpaChoice?.grandpaRole ?? GrandpaRole.None;
    public ParticipantSnapshot LocalGrandpaChoice => Snapshot.players.FirstOrDefault(player => player.id == identity);
    public int GrandpaChoiceRemaining => Mathf.CeilToInt((float)System.Math.Max(0, Snapshot.grandpaSelectionDeadline - GrandpaClock));
    private double GrandpaClock => manager.TimeManager.TicksToTime(manager.TimeManager.Tick);

    public void VolunteerForGrandpa(bool volunteer) => Send(SessionAction.GrandpaVolunteer, volunteer);
    public async void ConfirmGrandpaClass()
    {
        var role = AssignedGrandpaRole;
        if (!ChoosingGrandpaClass || role == GrandpaRole.None) return;
        if (!Offline)
        {
            Send(SessionAction.GrandpaConfirmClass, target: DefaultClassId(role));
            return;
        }
        if (Busy) return;

        var scene = offlineGrandpaClassScene;
        offlineGrandpaClassScene = default;
        try
        {
            var player = SpawnOfflineAvatar(scene, new Vector3(0, .05f, -3));
            player.GetComponent<GrayboxPlayerController>().PigClassSpeedMultiplier =
                PigClassPreset.Find(PigClassPreset.DefaultId)?.MovementMultiplier ?? 1f;
            MenuVisible = false;
            Report(SessionGameModes.Title(SelectedGame) + ". Esc — menu.");
        }
        catch (System.Exception error) { await StopSession(); Report(error.Message); }
    }

    private static string DefaultClassId(GrandpaRole role) =>
        role == GrandpaRole.Hunter ? DefaultHunterClassId : PigClassPreset.DefaultId;

    private void BeginGrandpaSelection()
    {
        grandpaSoloChoice = members.Count == 1;
        phase = grandpaSoloChoice ? SessionPhase.ChoosingGrandpaClasses : SessionPhase.ChoosingGrandpaRoles;
        grandpaSelectionDeadline = 0;
        grandpaLoadDeadline = GrandpaClock + GrandpaLoadTimeout;
        foreach (var member in members.Values)
        {
            member.WantsGrandpa = false;
            member.GrandpaRole = grandpaSoloChoice ? GrandpaRole.Pig : GrandpaRole.None;
            member.GrandpaClassReady = false;
            member.PigClassId = PigClassPreset.DefaultId;
        }
    }

    private bool InGrandpaHouse(Member member) => member.Connection?.IsActive == true &&
        member.Connection.Scenes.Contains(roundScene);

    private bool CanChooseGrandpaRole(Member member) => phase == SessionPhase.ChoosingGrandpaRoles &&
        InGrandpaHouse(member) && (grandpaSelectionDeadline == 0 || GrandpaClock < grandpaSelectionDeadline);

    private bool CanConfirmGrandpaClass(Member member, string id) => phase == SessionPhase.ChoosingGrandpaClasses &&
        member.GrandpaRole != GrandpaRole.None && !member.GrandpaClassReady && InGrandpaHouse(member) &&
        id == DefaultClassId(member.GrandpaRole) &&
        (grandpaSelectionDeadline == 0 || GrandpaClock < grandpaSelectionDeadline);

    private void TickGrandpaSelection()
    {
        if (!hosting) return;
        if (phase == SessionPhase.ChoosingGrandpaClasses) TickGrandpaClasses();
        else if (phase == SessionPhase.ChoosingGrandpaRoles) TickGrandpaRoles();
    }

    private void TickGrandpaRoles()
    {
        if (members.Values.Count(member => member.Connection?.IsActive == true) < 2)
        {
            ReturnToLobby();
            return;
        }

        double now = GrandpaClock;
        if (grandpaSelectionDeadline == 0)
        {
            if (now < grandpaLoadDeadline && members.Values.Any(member =>
                member.Connection?.IsActive == true && !InGrandpaHouse(member))) return;
            grandpaSelectionDeadline = now + GrandpaChoiceSeconds;
            Publish();
            return;
        }
        if (now < grandpaSelectionDeadline) return;

        var loadedPlayers = members.Values.Where(InGrandpaHouse).ToArray();
        if (loadedPlayers.Length < 2)
        {
            ReturnToLobby();
            return;
        }

        var volunteers = loadedPlayers.Where(player => player.WantsGrandpa).ToArray();
        var candidates = volunteers.Length > 0 ? volunteers : loadedPlayers;
        var hunter = candidates[Random.Range(0, candidates.Length)];
        foreach (var member in members.Values)
            member.GrandpaRole = member == hunter ? GrandpaRole.Hunter : GrandpaRole.Pig;

        phase = SessionPhase.ChoosingGrandpaClasses;
        grandpaSelectionDeadline = now + GrandpaClassSeconds;
        Publish();
    }

    private void TickGrandpaClasses()
    {
        var loadedPlayers = members.Values.Where(InGrandpaHouse).ToArray();
        if (!grandpaSoloChoice && (loadedPlayers.Length < 2 ||
            !loadedPlayers.Any(player => player.GrandpaRole == GrandpaRole.Hunter)))
        {
            ReturnToLobby();
            return;
        }
        if (loadedPlayers.Length == 0) return;

        bool allReady = loadedPlayers.All(player => player.GrandpaClassReady);
        bool timedOut = grandpaSelectionDeadline > 0 && GrandpaClock >= grandpaSelectionDeadline;
        if (!allReady && !timedOut) return;

        phase = SessionPhase.Round;
        grandpaSelectionDeadline = 0;
        Publish();
        foreach (var member in loadedPlayers)
            SpawnMember(member, member.Connection);
    }

    private Vector3 GrandpaSpawnPosition(Member member)
    {
        if (member.GrandpaRole == GrandpaRole.Hunter)
        {
            var spawn = roundScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(point => point.name == "GrandpaSpawn");
            return spawn != null ? spawn.position : new Vector3(-5.5f, .05f, 13f);
        }

        // Keep all pigs in one starting group, including when the hunter's lobby slot is in the middle.
        int slot = members.Values.Where(player => player.GrandpaRole == GrandpaRole.Pig)
            .OrderBy(player => player.ColorSlot).ToList().IndexOf(member);
        return new Vector3((slot % 3 - 1) * 1.5f, .05f, -3 + slot / 3 * 1.5f);
    }
}
