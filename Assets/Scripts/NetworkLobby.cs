using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using UnityEngine.SceneManagement;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

[RequireComponent(typeof(NetworkManager))]
public sealed class NetworkLobby : MonoBehaviour
{
    private sealed class Member
    {
        public string Id, Name;
        public bool Ready, Spectator, FinishReady, Eliminated;
        public NetworkConnection Connection;
        public NetworkObject Avatar;
        public float ReservedUntil;
        public float NextReaction;
        public int Appearance;
    }
    public const int Capacity = 6;
    public const float ReconnectGrace = 60f;
    public const int ProtocolVersion = 10;
    public static NetworkLobby Instance { get; private set; }
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private GameObject offlinePlayerPrefab;
    [SerializeField] private Font menuFont;
    public Font MenuFont => menuFont;
    [SerializeField] private ushort defaultPort = 7770;
    public SessionSnapshot Snapshot { get; private set; } = new SessionSnapshot();
    public string Status { get; private set; } = "Create a lobby or join with a friend's code.";
    public string Nickname { get; set; } = "Pig";
    public bool Busy { get; private set; }
    public bool Offline { get; private set; }
    private OutfitRoundResults roundResults, offlineResults;
    // JsonUtility materializes empty inline classes when a null result crosses the wire.
    // The session phase determines whether that payload is an actual report.
    public OutfitRoundResults Results => Offline ? (offlineResults != null && offlineResults.round > 0 ? offlineResults : null) :
        Snapshot.phase == SessionPhase.Results || Snapshot.phase == SessionPhase.Returning ? Snapshot.results : null;
    public bool MenuVisible { get; set; } = true;
    public bool InventoryOpen { get; set; }
    public bool CargoOpen { get; set; }
    public bool InSession => Snapshot.phase != SessionPhase.Menu;
    public bool IsHost => hosting;
    public bool IsEliminated => Offline ? PlayerRegistry.Players.Any(p => p != null && p.IsLocal && !p.IsAlive) :
        Snapshot.players.Any(p => p.id == identity && p.eliminated);
    public bool IsSpectator => IsEliminated || Snapshot.players.Any(p => p.id == identity && p.spectator);
    public bool InputAllowed => !EmoteWheel.BlocksInput && Results == null && !MenuVisible && !InventoryOpen && !CargoOpen && !IsSpectator && (Offline || Snapshot.phase == SessionPhase.Round);
    public bool IsFinishReady => Snapshot.players.Any(p => p.id == identity && p.finishReady);
    public bool NearFinish => PlayerRegistry.Players.Any(p => p != null && p.IsLocal && RoundFinishStation.ForScene(p.gameObject.scene)?.Contains(p) == true);
    public string Identity => identity;
    public int SelectedAppearance => PigFace.Selected;
    public void SelectAppearance(int value)
    {
        if (Busy || Offline || InSession && Snapshot.phase != SessionPhase.Lobby) return;
        PigFace.SaveSelection(value);
        if (InSession) Send(SessionAction.Appearance);
    }
    public event Action Changed;
    private readonly Dictionary<string, Member> members = new Dictionary<string, Member>();
    private readonly Dictionary<NetworkConnection, Member> connections = new Dictionary<NetworkConnection, Member>();
    private readonly HashSet<string> banned = new HashSet<string>();
    private NetworkManager manager;
    private EosSession eos;
    private Multipass multipass;
    private Scene roundScene;
    private SessionPhase phase;
    private int round = 1;
    private bool hosting, online, intentionalStop, closing, admissionClosed;
    private string identity, code, hostIdentity;
    private Coroutine reconnect;
    private SessionCheckpoint resume;
    private int operation;
    private Task connectTask;
    private bool cancelling;
    private float connectionDeadline;
    private bool awaitingHello;
    private float nextFinishCheck;
    private string runwayReaction;
    private int runwayReactionSequence;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        manager = GetComponent<NetworkManager>(); multipass = GetComponent<Multipass>();
        // Cleanup can run before online authentication selects the EOS transport.
        multipass.SetClientTransport<Tugboat>();
        eos = gameObject.AddComponent<EosSession>();
        eos.SessionClosed += EndFromService;
        Nickname = PlayerPrefs.GetString("svinki.nickname", "Pig");
        manager.ServerManager.RegisterBroadcast<SessionRequest>(OnRequest);
        manager.ClientManager.RegisterBroadcast<SessionMessage>(OnMessage);
        manager.ServerManager.OnRemoteConnectionState += OnRemoteState;
        manager.ClientManager.OnClientConnectionState += OnClientState;
        manager.SceneManager.OnLoadEnd += OnLoadEnd;
        manager.SceneManager.OnUnloadEnd += OnUnloadEnd;
        manager.SceneManager.OnClientPresenceChangeEnd += OnPresence;
        gameObject.AddComponent<SessionMenu>();
        gameObject.AddComponent<SpectatorCamera>();
#if UNITY_EDITOR || DEBUG
        gameObject.AddComponent<SessionDevelopmentDriver>();
#endif
    }
    private void Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        string Value(string key) { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        if (ushort.TryParse(Value("--port"), out ushort port)) defaultPort = port;
        if (!string.IsNullOrEmpty(Value("--nickname"))) Nickname = Value("--nickname");
        if (args.Contains("--local-host")) DebugHost();
        else if (args.Contains("--local-join")) DebugJoin(Value("--address") ?? "127.0.0.1");
    }
    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null; intentionalStop = true; closing = true;
        manager.ServerManager.UnregisterBroadcast<SessionRequest>(OnRequest);
        manager.ClientManager.UnregisterBroadcast<SessionMessage>(OnMessage);
        manager.ServerManager.OnRemoteConnectionState -= OnRemoteState;
        manager.ClientManager.OnClientConnectionState -= OnClientState;
        manager.SceneManager.OnLoadEnd -= OnLoadEnd;
        manager.SceneManager.OnUnloadEnd -= OnUnloadEnd;
        manager.SceneManager.OnClientPresenceChangeEnd -= OnPresence;
    }
    private void OnApplicationQuit()
    {
        intentionalStop = true; closing = true;
        if (!hosting || manager == null || !manager.ServerManager.Started) return;
        foreach (NetworkConnection connection in connections.Where(p => p.Value.Id != hostIdentity).Select(p => p.Key).ToArray())
            if (connection.IsActive) manager.ServerManager.Broadcast(connection, new SessionMessage
                { Terminal = true, Error = "The host ended the game. The host keeps the checkpoint." });
        manager.ServerManager.StopConnection(true);
    }
    public void SetPlaying(bool value)
    {
        if (value) MenuVisible = false;
        Changed?.Invoke();
    }
    public void Report(string message) { Status = message; Changed?.Invoke(); }
    private void Prepare(bool host, bool useOnline, bool continuing)
    {
        if (cancelling || Busy || manager.ClientManager.Started || manager.ServerManager.Started || Offline) throw new InvalidOperationException("Leave the current session first.");
        resume = continuing ? CheckpointStore.Read() : null;
        round = resume?.round ?? 1;
        hosting = host; online = useOnline; intentionalStop = false; closing = false;
        admissionClosed = false; members.Clear(); connections.Clear(); banned.Clear();
        Snapshot = new SessionSnapshot(); phase = SessionPhase.Lobby;
        roundResults = null; offlineResults = null; InventoryOpen = false; CargoOpen = false;
        Nickname = CleanNickname(Nickname); PlayerPrefs.SetString("svinki.nickname", Nickname); PlayerPrefs.Save();
    }
    public void CreateOnline(bool continuing = false) => BeginOnline(true, null, continuing);
    public void JoinOnline(string roomCode) => BeginOnline(false, roomCode, false);
    private async void BeginOnline(bool host, string roomCode, bool continuing)
    {
        if (Busy || cancelling || closing || InSession || Offline) return;
        connectTask = ConnectOnline(host, roomCode, continuing);
        await connectTask; connectTask = null;
    }
    private async Task ConnectOnline(bool host, string roomCode, bool continuing)
    {
        int token = ++operation;
        try
        {
            Prepare(host, true, continuing); Busy = true; Report("Connecting to EOS…");
            await eos.Authenticate(Nickname);
            if (token != operation) { await eos.Leave(); return; }
            identity = eos.Identity;
            if (host) await eos.Create(); else await eos.Join(roomCode);
            if (token != operation) { await eos.Leave(); return; }
            code = eos.Code; hostIdentity = eos.HostIdentity;
#if !UNITY_WEBGL && !EOS_DISABLE
            var transport = multipass.GetTransport<FishNet.Transporting.FishyEOSPlugin.FishyEOS>();
            transport.SocketName = "SvinkiV1"; transport.RemoteProductUserId = hostIdentity;
            multipass.SetClientTransport(transport);
            if (host && !multipass.StartConnection(true, transport.Index)) throw new InvalidOperationException("Could not start the host.");
#endif
            if (!manager.ClientManager.StartConnection()) throw new InvalidOperationException("Could not start the connection.");
            connectionDeadline = Time.unscaledTime + 25f; awaitingHello = true;
            Report("Connecting to the host…");
        }
        catch (Exception error)
        {
            if (token == operation) { await StopSession(); Report(error.Message); }
        }
        finally { if (token == operation) { if (!awaitingHello) Busy = false; Changed?.Invoke(); } }
    }
    public void DebugHost(bool continuing = false) => StartLocal(true, "127.0.0.1", continuing);
    public void DebugJoin(string address = "127.0.0.1") => StartLocal(false, address, false);
    private void StartLocal(bool host, string address, bool continuing)
    {
        try
        {
            Prepare(host, false, continuing); Busy = true; identity = DebugIdentity();
            code = host ? "LOCAL1" : ""; hostIdentity = host ? identity : "";
            var transport = multipass.GetTransport<Tugboat>(); transport.SetPort(defaultPort);
            multipass.SetClientTransport(transport);
            if (host && !multipass.StartConnection(true, transport.Index)) throw new InvalidOperationException("The local test port is in use.");
            if (!manager.ClientManager.StartConnection(address, defaultPort)) throw new InvalidOperationException("The local test could not connect.");
            awaitingHello = true; connectionDeadline = Time.unscaledTime + 25f;
            Report("Local test: connecting…");
        }
        catch (Exception error) { Busy = false; EndFromService(error.Message); }
    }
    private string DebugIdentity()
    {
        string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "--profile");
        string key = "svinki.local-id." + (i >= 0 && i + 1 < args.Length ? args[i + 1] : Application.isEditor ? "editor" : "build");
        string id = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(id)) { id = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(key, id); PlayerPrefs.Save(); }
        return id;
    }
    private static string CleanNickname(string value) => string.IsNullOrWhiteSpace(value) ? "Pig" : new string(value.Trim().Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(24).ToArray());
    public void Ready(bool value) => Send(SessionAction.Ready, value);
    public void StartRound() => Send(SessionAction.Start);
    public void EndRound() => FinishReady(true);
    public void FinishReady(bool value)
    {
        if (!Offline) { Send(SessionAction.FinishReady, value); return; }
        if (value && Results == null && !IsEliminated)
        {
            if (!NearFinish) { Report("Return to the Ready button in the starting room."); return; }
            PresentOfflineOutfit();
        }
    }
    private void PresentOfflineOutfit()
    {
        if (Results != null) return;
        var rules = OutfitRatingCatalog.Load();
        if (rules == null) { Report("Outfit rating rules could not be found."); return; }
        var player = PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal);
        if (player == null) return;
        offlineResults = new OutfitRoundResults { round = 1, entries = new[] {
            OutfitRoundResults.Rate(rules, "offline", Nickname,
                player.GetComponentInChildren<PigAppearance>(true)?.FaceCode ?? SelectedAppearance,
                player.Outfit.Items, !player.IsAlive) } };
        InventoryOpen = false; CargoOpen = false; MenuVisible = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Changed?.Invoke();
    }
    public void ContinueAfterResults()
    {
        if (!Offline) { Send(SessionAction.Continue); return; }
        if (IsEliminated) { RestartOfflineRound(); return; }
        offlineResults = null; MenuVisible = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; Changed?.Invoke();
    }
    public void ToggleAdmission() => Send(SessionAction.ToggleAdmission);
    public static string ReactionText(string kind) => kind == "clap" ? "Applause!" : kind == "heart" ? "Love this outfit!" : kind == "oink" ? "Oink-oink!" : "";
    public void RunwayReact(string kind) { if (!Offline && Results != null) Send(SessionAction.RunwayReact, false, kind); }
    public void RunwayPose(int pose)
    {
        if (Results == null || pose < 0 || pose > 2) return;
        if (Offline) { foreach (var entry in offlineResults.entries) entry.presentationPose = pose; }
        else Send(SessionAction.RunwayPose, false, pose.ToString());
    }
    public void Kick(string target) => Send(SessionAction.Kick, false, target);
    private void Send(SessionAction action, bool value = false, string target = null)
    {
        if (manager.ClientManager.Started) manager.ClientManager.Broadcast(new SessionRequest
        { Version = ProtocolVersion, Action = action, Identity = identity, Nickname = Nickname, Value = value,
          Target = target, Appearance = SelectedAppearance });
    }
    private void OnClientState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started) Send(SessionAction.Hello);
        if (args.ConnectionState != LocalConnectionState.Stopped || intentionalStop || closing) return;
        MenuVisible = true;
        if (hosting || !InSession) { EndFromService("Connection closed. The host keeps the checkpoint."); return; }
        if (reconnect == null) reconnect = StartCoroutine(Reconnect());
    }
    private IEnumerator Reconnect()
    {
        float deadline = Time.unscaledTime + ReconnectGrace;
        var stale = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++) { var scene = SceneManager.GetSceneAt(i); if (scene.name == "SampleScene") stale.Add(scene); }
        foreach (Scene scene in stale) { var unload = SceneManager.UnloadSceneAsync(scene); if (unload != null) while (!unload.isDone) yield return null; }
        Report("Connection lost. Reconnecting (up to 60 seconds)…");
        while (!intentionalStop && Time.unscaledTime < deadline)
        {
            yield return new WaitForSecondsRealtime(3f);
            if (manager.ClientManager.Started) { reconnect = null; yield break; }
            LocalConnectionState state = multipass.GetConnectionState(false);
            if (state == LocalConnectionState.Stopped) manager.ClientManager.StartConnection();
        }
        reconnect = null;
        if (!intentionalStop) EndFromService("Reconnection failed. You can rejoin by code as a spectator.");
    }
    private void OnMessage(SessionMessage message, Channel channel)
    {
        if (!string.IsNullOrEmpty(message.Error)) Report(message.Error);
        if (message.Terminal) { EndFromService(message.Error); return; }
        if (string.IsNullOrEmpty(message.Snapshot)) return;
        Snapshot = JsonUtility.FromJson<SessionSnapshot>(message.Snapshot);
        awaitingHello = false; Busy = false;
        bool inRound = Snapshot.phase == SessionPhase.Round;
        if (!inRound) MenuVisible = true;
        if (inRound && IsSpectator) MenuVisible = false;
        if (Snapshot.phase == SessionPhase.Results) { InventoryOpen = false; CargoOpen = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        Report(Snapshot.phase == SessionPhase.Results ? "Outfit results for round " + Snapshot.round + "." :
            inRound ? IsSpectator ? "Spectating until the next round. ← / → — switch player; Esc — menu." : "Round " + Snapshot.round + ". Esc — menu." : "Lobby " + Snapshot.code + ". Mark yourself ready.");
    }
    private void OnRequest(NetworkConnection connection, SessionRequest request, Channel channel)
    {
        if (request.Action == SessionAction.Hello)
        {
            if (connections.ContainsKey(connection)) return;
            string id = request.Identity;
            if (request.Version != ProtocolVersion || string.IsNullOrEmpty(id) || id.Length > 64) { Reject(connection, "Incompatible game version."); return; }
            if (online && manager.TransportManager.Transport.GetConnectionAddress(connection.ClientId) != id) { Reject(connection, "Could not verify the guest profile."); return; }
            if (banned.Contains(id)) { Reject(connection, "The host removed you from this session."); return; }
            members.TryGetValue(id, out Member member);
            if (member != null && member.Connection != null) { Reject(connection, "This profile is already connected."); return; }
            bool returning = member != null && member.ReservedUntil > Time.unscaledTime;
            if (!returning && (admissionClosed || members.Count >= Capacity || phase == SessionPhase.Loading || phase == SessionPhase.Returning))
            { Reject(connection, admissionClosed ? "The host closed the lobby to new players." : "The lobby is full or changing rounds. Try again later."); return; }
            if (member == null)
            {
                member = new Member { Id = id, Name = CleanNickname(request.Nickname), Spectator = phase == SessionPhase.Round,
                    Appearance = PigFace.Sanitize(request.Appearance) };
                members.Add(id, member);
            }
            member.Connection = connection; member.ReservedUntil = 0; member.Ready = false;
            connections.Add(connection, member);
            Publish();
            if (roundScene.IsValid() && phase == SessionPhase.Round) manager.SceneManager.LoadConnectionScenes(connection, new SceneLoadData(roundScene));
            return;
        }
        if (!connections.TryGetValue(connection, out Member caller)) return;
        switch (request.Action)
        {
            case SessionAction.Appearance:
                if (phase == SessionPhase.Lobby)
                {
                    caller.Appearance = PigFace.Sanitize(request.Appearance);
                    caller.Ready = false;
                }
                break;
            case SessionAction.Ready:
                if (phase == SessionPhase.Lobby) caller.Ready = request.Value;
                break;
            case SessionAction.Start:
                if (caller.Id == hostIdentity) BeginRound();
                break;
            case SessionAction.EndRound:
            case SessionAction.FinishReady:
                if (phase == SessionPhase.Round && !caller.Spectator && !caller.Eliminated && caller.Avatar != null)
                {
                    bool ready = request.Action == SessionAction.EndRound || request.Value;
                    var avatar = caller.Avatar.GetComponent<PlayerAvatar>();
                    if (!ready || RoundFinishStation.ForScene(roundScene)?.Contains(avatar) == true) caller.FinishReady = ready;
                    else manager.ServerManager.Broadcast(connection, new SessionMessage { Error = "Go to the Ready button in the starting room." });
                    TryFinishRound();
                }
                break;
            case SessionAction.RunwayReact:
                if (phase == SessionPhase.Results && Time.unscaledTime >= caller.NextReaction && ReactionText(request.Target).Length > 0)
                {
                    caller.NextReaction = Time.unscaledTime + .65f;
                    runwayReaction = caller.Name + ": " + ReactionText(request.Target); runwayReactionSequence++;
                }
                break;
            case SessionAction.RunwayPose:
                if (phase == SessionPhase.Results && int.TryParse(request.Target, out int pose) && pose >= 0 && pose <= 2)
                {
                    var entry = roundResults?.entries.FirstOrDefault(p => p.id == caller.Id);
                    if (entry != null) entry.presentationPose = pose;
                }
                break;
            case SessionAction.Continue:
                if (caller.Id == hostIdentity && phase == SessionPhase.Results) ReturnToLobby();
                break;
            case SessionAction.ToggleAdmission:
                if (caller.Id == hostIdentity) admissionClosed = !admissionClosed;
                break;
            case SessionAction.Kick:
                if (caller.Id == hostIdentity && request.Target != hostIdentity && members.TryGetValue(request.Target ?? "", out Member kicked))
                {
                    banned.Add(kicked.Id); RemoveMember(kicked); eos.Kick(kicked.Id);
                    if (kicked.Connection != null) Reject(kicked.Connection, "The host removed you from the session.");
                }
                break;
            case SessionAction.Leave:
                if (caller.Id != hostIdentity) RemoveMember(caller);
                break;
        }
        Publish();
    }
    private void Reject(NetworkConnection connection, string message)
    {
        manager.ServerManager.Broadcast(connection, new SessionMessage { Error = message, Terminal = true });
        StartCoroutine(DisconnectSoon(connection));
    }
    private IEnumerator DisconnectSoon(NetworkConnection connection)
    {
        yield return new WaitForSecondsRealtime(0.3f);
        if (connection.IsActive) connection.Disconnect(true);
    }
    private void BeginRound()
    {
        if (phase != SessionPhase.Lobby || members.Count == 0 || members.Values.Any(p => p.Connection == null || !p.Ready)) return;
        try
        {
            var defaultItems = playerPrefab.GetComponent<PlayerOutfit>().StartingItems.Select(p => p.name).ToArray();
            CheckpointStore.Write(new SessionCheckpoint
            {
                round = round,
                players = members.Values.Select(p => new CheckpointPlayer { id = p.Id, nickname = p.Name,
                    outfit = resume?.players.FirstOrDefault(saved => saved.id == p.Id)?.outfit ?? defaultItems }).ToArray()
            });
        }
        catch (Exception error) { Report("Could not start the round: checkpoint could not be saved. " + error.Message); return; }
        phase = SessionPhase.Loading;
        foreach (Member member in members.Values) { member.Spectator = false; member.FinishReady = false; member.Eliminated = false; }
        // One host runs one round. NavMesh collider collection requires the default physics world.
        var data = new SceneLoadData("SampleScene");
        data.Options.AllowStacking = false;
        manager.SceneManager.LoadConnectionScenes(connections.Keys.ToArray(), data);
        Publish();
    }
    private void OnLoadEnd(SceneLoadEndEventArgs args)
    {
        // Additive FishNet loads otherwise keep the bright Lobby RenderSettings active.
        Scene presentation = args.LoadedScenes.FirstOrDefault(scene => scene.name == "SampleScene");
        if (!presentation.IsValid() && args.SkippedSceneNames.Contains("SampleScene"))
            presentation = SceneManager.GetSceneByName("SampleScene");
        if (presentation.IsValid() && presentation.isLoaded) SceneManager.SetActiveScene(presentation);
        if (!args.QueueData.AsServer || phase != SessionPhase.Loading) return;
        Scene loaded = args.LoadedScenes.FirstOrDefault(scene => scene.name == "SampleScene");
        if (!loaded.IsValid() && args.SkippedSceneNames.Contains("SampleScene"))
            loaded = SceneManager.GetSceneByName("SampleScene");
        if (!loaded.IsValid() || !loaded.isLoaded) return;
        roundScene = loaded; RoundClothingLayout.Begin(roundScene); RoundMonkeySpawner.Begin(roundScene, members.Values.Count(p => !p.Spectator)); phase = SessionPhase.Round; Publish();
    }
    private void OnPresence(ClientPresenceChangeEventArgs args)
    {
        if (!args.Added || args.Scene != roundScene || !connections.TryGetValue(args.Connection, out Member member) || member.Spectator) return;
        if (member.Avatar != null)
        {
            member.Avatar.GiveOwnership(args.Connection); return;
        }
        int slot = members.Values.ToList().IndexOf(member);
        NetworkObject player = Instantiate(playerPrefab, new Vector3((slot % 3 - 1) * 1.5f, .05f, -3 + slot / 3 * 1.5f), Quaternion.identity);
        string[] saved = resume?.players.FirstOrDefault(p => p.id == member.Id)?.outfit;
        if (saved != null) player.GetComponent<NetworkPlayer>().RestoreOutfit(saved);
        player.GetComponent<NetworkPlayer>().SetIdentity(member.Id, member.Name);
        player.GetComponent<NetworkPlayer>().SetAppearance(member.Appearance);
        manager.ServerManager.Spawn(player, args.Connection, roundScene); member.Avatar = player;
        if (member.Eliminated) player.GetComponent<NetworkPlayer>().RestoreEliminated();
    }
    private void ReturnToLobby()
    {
        phase = SessionPhase.Returning;
        foreach (Member member in members.Values)
        {
            if (member.Avatar != null) manager.ServerManager.Despawn(member.Avatar);
            member.Avatar = null; member.Ready = false; member.Spectator = false; member.FinishReady = false; member.Eliminated = false;
        }
        Publish(); manager.SceneManager.UnloadConnectionScenes(connections.Keys.ToArray(), new SceneUnloadData(roundScene));
    }
    private void PresentOutfits()
    {
        var rules = OutfitRatingCatalog.Load();
        if (rules == null) { Report("Outfit rating rules could not be found."); return; }
        // Capture the server's inventories before anything is despawned or a thief changes them.
        var entries = members.Values.Where(p => !p.Spectator && p.Avatar != null).Select(p =>
            OutfitRoundResults.Rate(rules, p.Id, p.Name, p.Appearance, p.Avatar.GetComponent<PlayerOutfit>().Items,
                p.Eliminated || p.Avatar.GetComponent<NetworkPlayer>().IsDead))
            .OrderByDescending(p => p.rating.score).ThenBy(p => p.nickname, StringComparer.Ordinal).ToArray();
        roundResults = new OutfitRoundResults { round = round, entries = entries };
        phase = SessionPhase.Results; Publish();
    }
    private void TryFinishRound()
    {
        if (!hosting || phase != SessionPhase.Round) return;
        var all = members.Values.Where(p => !p.Spectator).ToArray();
        if (all.Length > 0 && all.All(p => p.Eliminated)) { PresentOutfits(); return; }
        var participants = all.Where(p => !p.Eliminated).ToArray();
        var station = RoundFinishStation.ForScene(roundScene);
        if (participants.Length > 0 && station != null && participants.All(p => p.Connection != null && p.FinishReady &&
            p.Avatar != null && station.Contains(p.Avatar.GetComponent<PlayerAvatar>()))) PresentOutfits();
    }
    public bool CanEditOutfit(NetworkPlayer player) => hosting && phase == SessionPhase.Round && player != null &&
        members.TryGetValue(player.ParticipantId, out Member member) && member.Avatar == player.NetworkObject && !member.Spectator && !member.Eliminated && !player.IsDead;
    public void PlayerEliminated(NetworkPlayer player)
    {
        if (!hosting || player == null || !player.IsDead || !members.TryGetValue(player.ParticipantId, out Member member)) return;
        member.Eliminated = true; member.FinishReady = false; Publish(); TryFinishRound();
    }
    public void RestartOfflineRound()
    {
        if (Offline && IsEliminated && !Busy) StartCoroutine(RestartOffline());
    }
    private IEnumerator RestartOffline()
    {
        Busy = true;
        Scene scene = SceneManager.GetSceneByName("SampleScene");
        if (scene.IsValid())
        {
            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone) yield return null;
        }
        Offline = false; offlineResults = null; Busy = false; StartOffline();
    }
    public void OutfitChanged(NetworkPlayer player)
    {
        if (CanEditOutfit(player) && members.TryGetValue(player.ParticipantId, out Member member) && member.FinishReady)
        { member.FinishReady = false; Publish(); }
    }
    private void OnUnloadEnd(SceneUnloadEndEventArgs args)
    {
        if (!args.QueueData.AsServer || phase != SessionPhase.Returning) return;
        roundScene = default; phase = SessionPhase.Lobby; round++; resume = null;
        roundResults = null;
        foreach (Member member in members.Values.Where(p => p.Connection == null).ToArray()) RemoveMember(member);
        Publish();
    }
    private void OnRemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Stopped || !connections.TryGetValue(connection, out Member member)) return;
        connections.Remove(connection); member.Connection = null; member.Ready = false; member.FinishReady = false;
        if (closing) return;
        if (member.Id == hostIdentity) { EndFromService("The host ended the session."); return; }
        if (phase == SessionPhase.Round)
        {
            member.ReservedUntil = Time.unscaledTime + ReconnectGrace;
            if (member.Avatar != null) member.Avatar.GetComponent<NetworkPlayer>().FreezeDisconnected();
        }
        else RemoveMember(member);
        Publish();
    }
    private void RemoveMember(Member member)
    {
        if (member.Avatar != null && member.Avatar.IsSpawned) manager.ServerManager.Despawn(member.Avatar);
        member.Avatar = null; members.Remove(member.Id);
        if (member.Connection != null) connections.Remove(member.Connection);
    }
    private void Publish()
    {
        if (!hosting) return;
        var snapshot = new SessionSnapshot
        {
            phase = phase, code = code, host = hostIdentity, round = round, closed = admissionClosed, results = roundResults, runwayReaction = runwayReaction, runwayReactionSequence = runwayReactionSequence,
            players = members.Values.Select(p => new ParticipantSnapshot { id = p.Id, nickname = p.Name,
                connected = p.Connection != null, ready = p.Ready, spectator = p.Spectator,
                reservation = Mathf.Max(0f, p.ReservedUntil - Time.unscaledTime), appearance = p.Appearance, finishReady = p.FinishReady, eliminated = p.Eliminated }).ToArray()
        };
        string json = JsonUtility.ToJson(snapshot);
        foreach (NetworkConnection connection in connections.Keys) if (connection.IsActive)
            manager.ServerManager.Broadcast(connection, new SessionMessage { Snapshot = json });
    }
    private void Update()
    {
        if (Offline && !Busy && IsEliminated && Results == null) PresentOfflineOutfit();
        if (hosting && phase == SessionPhase.Round && Time.unscaledTime >= nextFinishCheck)
        {
            nextFinishCheck = Time.unscaledTime + .2f;
            bool changed = false;
            var station = RoundFinishStation.ForScene(roundScene);
            foreach (Member member in members.Values.Where(p => p.FinishReady))
                if (member.Connection == null || member.Avatar == null || station?.Contains(member.Avatar.GetComponent<PlayerAvatar>()) != true)
                { member.FinishReady = false; changed = true; }
            if (changed) Publish();
            TryFinishRound();
        }
        if (hosting && members.Values.Any(p => p.Connection == null && p.ReservedUntil > 0 && p.ReservedUntil <= Time.unscaledTime))
        {
            foreach (Member p in members.Values.Where(p => p.Connection == null && p.ReservedUntil > 0 && p.ReservedUntil <= Time.unscaledTime).ToArray()) RemoveMember(p);
            Publish();
        }
        if (awaitingHello && Time.unscaledTime > connectionDeadline)
        { awaitingHello = false; EndFromService("The host did not respond. Check the code and connection, then try again."); }
        if (Results == null && !OutfitInventoryView.ConsumedEscape && !CartCargoView.ConsumedEscape && !InventoryOpen && !CargoOpen && !PlayerChat.ConsumedEscape && !EmoteWheel.ConsumedEscape && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true && (Snapshot.phase == SessionPhase.Round || Offline))
        {
            MenuVisible = !MenuVisible;
            Cursor.lockState = MenuVisible ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = MenuVisible;
            Changed?.Invoke();
        }
    }
    public async void Leave()
    {
        ++operation;
        if (!hosting) Send(SessionAction.Leave);
        else if (manager.ServerManager.Started)
            foreach (NetworkConnection connection in connections.Where(p => p.Value.Id != hostIdentity).Select(p => p.Key).ToArray())
                if (connection.IsActive) manager.ServerManager.Broadcast(connection, new SessionMessage
                    { Terminal = true, Error = "The host ended the session. The host keeps the checkpoint." });
        await Task.Delay(150);
        await StopSession(); Report("Session ended. The host saved the checkpoint.");
    }
    public async void Cancel()
    {
        cancelling = true; ++operation;
        Task pending = connectTask;
        await StopSession();
        if (pending != null) { Busy = true; Report("Cancelling connection…"); await pending; }
        Busy = false; cancelling = false; Report("Connection cancelled.");
    }
    private async void EndFromService(string message) { ++operation; await StopSession(); Report(message); }
    private async Task StopSession()
    {
        if (closing) return;
        closing = true; intentionalStop = true; awaitingHello = false;
        if (reconnect != null) { StopCoroutine(reconnect); reconnect = null; }
        if (Offline)
        {
            Offline = false;
            Scene scene = SceneManager.GetSceneByName("SampleScene"); if (scene.IsValid()) await UnloadOffline(scene);
        }
        else
        {
            manager.ClientManager.StopConnection();
            if (hosting) manager.ServerManager.StopConnection(true);
        }
        // FishNet resets its scene bookkeeping on shutdown; client-loaded scenes still need explicit cleanup.
        var stale = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++) { var scene = SceneManager.GetSceneAt(i); if (scene.name == "SampleScene") stale.Add(scene); }
        foreach (Scene scene in stale) if (scene.IsValid() && scene.isLoaded) await UnloadOffline(scene);
        await eos.Leave();
        hosting = false; Busy = false; MenuVisible = true; members.Clear(); connections.Clear(); roundScene = default;
        Snapshot = new SessionSnapshot(); phase = SessionPhase.Menu; closing = false; roundResults = null; offlineResults = null; InventoryOpen = false; CargoOpen = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Changed?.Invoke();
    }
    private static async Task UnloadOffline(Scene scene)
    {
        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene); if (unload == null) return; while (!unload.isDone) await Task.Yield();
    }
    public async void StartOffline()
    {
        if (Busy || InSession || Offline) return;
        int token = ++operation;
        Busy = true; Offline = true; offlineResults = null; Report("Loading single player…");
        try
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
            while (!load.isDone) await Task.Yield();
            Scene scene = SceneManager.GetSceneByName("SampleScene");
            if (token != operation) { if (scene.IsValid() && scene.isLoaded) await UnloadOffline(scene); return; }
            SceneManager.SetActiveScene(scene);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (NetworkBehaviour component in root.GetComponentsInChildren<NetworkBehaviour>(true)) component.enabled = false;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (NetworkObject component in root.GetComponentsInChildren<NetworkObject>(true))
                { component.SetIsNetworked(false); component.gameObject.SetActive(true); }
            RoundClothingLayout.Begin(scene);
            // Keep FishNet's cached component references valid when the scene unloads.
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (UnityEngine.AI.NavMeshAgent agent in root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) agent.enabled = true;
            GameObject player = Instantiate(offlinePlayerPrefab, new Vector3(0f, .05f, -3f), Quaternion.identity);
            player.GetComponentInChildren<PigAppearance>(true)?.Apply(SelectedAppearance);
            SceneManager.MoveGameObjectToScene(player, scene);
            player.GetComponent<WorldOutfitRenderer>()?.SetFirstPersonHidden(true);
            Camera.main.GetComponent<GrayboxFirstPersonCamera>().SetTarget(player.transform);
            FindAnyObjectByType<MannequinWardrobe>()?.Bind(player.GetComponent<PlayerOutfit>());
            RoundMonkeySpawner.Begin(scene, 1);
            MenuVisible = false; Report("Single player. Esc — menu.");
        }
        catch (Exception error) { await StopSession(); Report(error.Message); }
        finally { Busy = false; Changed?.Invoke(); }
    }
}
