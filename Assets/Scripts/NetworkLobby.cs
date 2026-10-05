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
        public bool Ready, Spectator;
        public NetworkConnection Connection;
        public NetworkObject Avatar;
        public float ReservedUntil;
    }
    public const int Capacity = 6;
    public const float ReconnectGrace = 60f;
    public static NetworkLobby Instance { get; private set; }
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private GameObject offlinePlayerPrefab;
    [SerializeField] private Font menuFont;
    public Font MenuFont => menuFont;
    [SerializeField] private ushort defaultPort = 7770;
    public SessionSnapshot Snapshot { get; private set; } = new SessionSnapshot();
    public string Status { get; private set; } = "Создайте лобби или войдите по коду друга.";
    public string Nickname { get; set; } = "Свинка";
    public bool Busy { get; private set; }
    public bool Offline { get; private set; }
    public bool MenuVisible { get; set; } = true;
    public bool InSession => Snapshot.phase != SessionPhase.Menu;
    public bool IsHost => hosting;
    public bool IsSpectator => Snapshot.players.Any(p => p.id == identity && p.spectator);
    public bool InputAllowed => !MenuVisible && (Offline || Snapshot.phase == SessionPhase.Round && !IsSpectator);
    public string Identity => identity;
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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        manager = GetComponent<NetworkManager>(); multipass = GetComponent<Multipass>();
        eos = gameObject.AddComponent<EosSession>();
        eos.SessionClosed += EndFromService;
        Nickname = PlayerPrefs.GetString("svinki.nickname", "Свинка");
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
                { Terminal = true, Error = "Хост завершил игру. Контрольная точка остаётся у хоста." });
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
        if (cancelling || Busy || manager.ClientManager.Started || manager.ServerManager.Started || Offline) throw new InvalidOperationException("Сначала выйдите из текущей сессии.");
        resume = continuing ? CheckpointStore.Read() : null;
        round = resume?.round ?? 1;
        hosting = host; online = useOnline; intentionalStop = false; closing = false;
        admissionClosed = false; members.Clear(); connections.Clear(); banned.Clear();
        Snapshot = new SessionSnapshot(); phase = SessionPhase.Lobby;
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
            Prepare(host, true, continuing); Busy = true; Report("Подключение к EOS…");
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
            if (host && !multipass.StartConnection(true, transport.Index)) throw new InvalidOperationException("Не удалось запустить хост.");
#endif
            if (!manager.ClientManager.StartConnection()) throw new InvalidOperationException("Не удалось начать подключение.");
            connectionDeadline = Time.unscaledTime + 25f; awaitingHello = true;
            Report("Подключение к хосту…");
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
            if (host && !multipass.StartConnection(true, transport.Index)) throw new InvalidOperationException("Порт локального теста занят.");
            if (!manager.ClientManager.StartConnection(address, defaultPort)) throw new InvalidOperationException("Локальный тест не подключился.");
            awaitingHello = true; connectionDeadline = Time.unscaledTime + 25f;
            Report("Локальный тест: подключение…");
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
    private static string CleanNickname(string value) => string.IsNullOrWhiteSpace(value) ? "Свинка" : new string(value.Trim().Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(24).ToArray());
    public void Ready(bool value) => Send(SessionAction.Ready, value);
    public void StartRound() => Send(SessionAction.Start);
    public void EndRound() => Send(SessionAction.EndRound);
    public void ToggleAdmission() => Send(SessionAction.ToggleAdmission);
    public void Kick(string target) => Send(SessionAction.Kick, false, target);
    private void Send(SessionAction action, bool value = false, string target = null)
    {
        if (manager.ClientManager.Started) manager.ClientManager.Broadcast(new SessionRequest
        { Version = 1, Action = action, Identity = identity, Nickname = Nickname, Value = value, Target = target });
    }
    private void OnClientState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started) Send(SessionAction.Hello);
        if (args.ConnectionState != LocalConnectionState.Stopped || intentionalStop || closing) return;
        MenuVisible = true;
        if (hosting || !InSession) { EndFromService("Соединение закрыто. Контрольная точка остаётся у хоста."); return; }
        if (reconnect == null) reconnect = StartCoroutine(Reconnect());
    }
    private IEnumerator Reconnect()
    {
        float deadline = Time.unscaledTime + ReconnectGrace;
        var stale = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++) { var scene = SceneManager.GetSceneAt(i); if (scene.name == "SampleScene") stale.Add(scene); }
        foreach (Scene scene in stale) { var unload = SceneManager.UnloadSceneAsync(scene); if (unload != null) while (!unload.isDone) yield return null; }
        Report("Связь потеряна. Пытаемся восстановить соединение (до 60 секунд)…");
        while (!intentionalStop && Time.unscaledTime < deadline)
        {
            yield return new WaitForSecondsRealtime(3f);
            if (manager.ClientManager.Started) { reconnect = null; yield break; }
            LocalConnectionState state = multipass.GetConnectionState(false);
            if (state == LocalConnectionState.Stopped) manager.ClientManager.StartConnection();
        }
        reconnect = null;
        if (!intentionalStop) EndFromService("Восстановить связь не удалось. Можно войти по коду снова как зритель.");
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
        Report(inRound ? IsSpectator ? "Наблюдение до следующего раунда. ← / → — сменить игрока; Esc — меню." : "Раунд " + Snapshot.round + ". Esc — меню." : "Лобби " + Snapshot.code + ". Отметьте готовность.");
    }
    private void OnRequest(NetworkConnection connection, SessionRequest request, Channel channel)
    {
        if (request.Action == SessionAction.Hello)
        {
            if (connections.ContainsKey(connection)) return;
            string id = request.Identity;
            if (request.Version != 1 || string.IsNullOrEmpty(id) || id.Length > 64) { Reject(connection, "Несовместимая версия игры."); return; }
            if (online && manager.TransportManager.Transport.GetConnectionAddress(connection.ClientId) != id) { Reject(connection, "Не удалось подтвердить гостевой профиль."); return; }
            if (banned.Contains(id)) { Reject(connection, "Хост исключил вас из этой сессии."); return; }
            members.TryGetValue(id, out Member member);
            if (member != null && member.Connection != null) { Reject(connection, "Этот профиль уже подключён."); return; }
            bool returning = member != null && member.ReservedUntil > Time.unscaledTime;
            if (!returning && (admissionClosed || members.Count >= Capacity || phase == SessionPhase.Loading || phase == SessionPhase.Returning))
            { Reject(connection, admissionClosed ? "Хост закрыл вход новым игрокам." : "Лобби заполнено или переключает раунд. Попробуйте позже."); return; }
            if (member == null)
            {
                member = new Member { Id = id, Name = CleanNickname(request.Nickname), Spectator = phase == SessionPhase.Round };
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
            case SessionAction.Ready:
                if (phase == SessionPhase.Lobby) caller.Ready = request.Value;
                break;
            case SessionAction.Start:
                if (caller.Id == hostIdentity) BeginRound();
                break;
            case SessionAction.EndRound:
                if (caller.Id == hostIdentity && phase == SessionPhase.Round) ReturnToLobby();
                break;
            case SessionAction.ToggleAdmission:
                if (caller.Id == hostIdentity) admissionClosed = !admissionClosed;
                break;
            case SessionAction.Kick:
                if (caller.Id == hostIdentity && request.Target != hostIdentity && members.TryGetValue(request.Target ?? "", out Member kicked))
                {
                    banned.Add(kicked.Id); RemoveMember(kicked); eos.Kick(kicked.Id);
                    if (kicked.Connection != null) Reject(kicked.Connection, "Хост исключил вас из сессии.");
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
        catch (Exception error) { Report("Раунд не запущен: не удалось сохранить контрольную точку. " + error.Message); return; }
        phase = SessionPhase.Loading;
        foreach (Member member in members.Values) member.Spectator = false;
        var data = new SceneLoadData("SampleScene"); data.Options.LocalPhysics = LocalPhysicsMode.Physics3D;
        data.Options.AllowStacking = true;
        manager.SceneManager.LoadConnectionScenes(connections.Keys.ToArray(), data);
        Publish();
    }
    private void OnLoadEnd(SceneLoadEndEventArgs args)
    {
        if (!args.QueueData.AsServer || phase != SessionPhase.Loading || args.LoadedScenes.Length == 0) return;
        roundScene = args.LoadedScenes[0]; phase = SessionPhase.Round; Publish();
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
        manager.ServerManager.Spawn(player, args.Connection, roundScene); member.Avatar = player;
    }
    private void ReturnToLobby()
    {
        phase = SessionPhase.Returning;
        foreach (Member member in members.Values)
        {
            if (member.Avatar != null) manager.ServerManager.Despawn(member.Avatar);
            member.Avatar = null; member.Ready = false; member.Spectator = false;
        }
        Publish(); manager.SceneManager.UnloadConnectionScenes(connections.Keys.ToArray(), new SceneUnloadData(roundScene));
    }
    private void OnUnloadEnd(SceneUnloadEndEventArgs args)
    {
        if (!args.QueueData.AsServer || phase != SessionPhase.Returning) return;
        roundScene = default; phase = SessionPhase.Lobby; round++; resume = null;
        foreach (Member member in members.Values.Where(p => p.Connection == null).ToArray()) RemoveMember(member);
        Publish();
    }
    private void OnRemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Stopped || !connections.TryGetValue(connection, out Member member)) return;
        connections.Remove(connection); member.Connection = null; member.Ready = false;
        if (closing) return;
        if (member.Id == hostIdentity) { EndFromService("Хост завершил сессию."); return; }
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
            phase = phase, code = code, host = hostIdentity, round = round, closed = admissionClosed,
            players = members.Values.Select(p => new ParticipantSnapshot { id = p.Id, nickname = p.Name,
                connected = p.Connection != null, ready = p.Ready, spectator = p.Spectator,
                reservation = Mathf.Max(0f, p.ReservedUntil - Time.unscaledTime) }).ToArray()
        };
        string json = JsonUtility.ToJson(snapshot);
        foreach (NetworkConnection connection in connections.Keys) if (connection.IsActive)
            manager.ServerManager.Broadcast(connection, new SessionMessage { Snapshot = json });
    }
    private void Update()
    {
        if (hosting && members.Values.Any(p => p.Connection == null && p.ReservedUntil > 0 && p.ReservedUntil <= Time.unscaledTime))
        {
            foreach (Member p in members.Values.Where(p => p.Connection == null && p.ReservedUntil > 0 && p.ReservedUntil <= Time.unscaledTime).ToArray()) RemoveMember(p);
            Publish();
        }
        if (awaitingHello && Time.unscaledTime > connectionDeadline)
        { awaitingHello = false; EndFromService("Хост не ответил. Проверьте код и соединение, затем повторите."); }
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true && (Snapshot.phase == SessionPhase.Round || Offline))
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
                    { Terminal = true, Error = "Хост завершил сессию. Контрольная точка остаётся у хоста." });
        await Task.Delay(150);
        await StopSession(); Report("Сессия завершена. Контрольная точка сохранена у хоста.");
    }
    public async void Cancel()
    {
        cancelling = true; ++operation;
        Task pending = connectTask;
        await StopSession();
        if (pending != null) { Busy = true; Report("Отменяем подключение…"); await pending; }
        Busy = false; cancelling = false; Report("Подключение отменено.");
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
        Snapshot = new SessionSnapshot(); phase = SessionPhase.Menu; closing = false;
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
        Busy = true; Offline = true; Report("Загрузка одиночной игры…");
        try
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
            while (!load.isDone) await Task.Yield();
            Scene scene = SceneManager.GetSceneByName("SampleScene");
            if (token != operation) { if (scene.IsValid() && scene.isLoaded) await UnloadOffline(scene); return; }
            SceneManager.SetActiveScene(scene);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (NetworkBehaviour component in root.GetComponentsInChildren<NetworkBehaviour>(true)) Destroy(component);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (NetworkObject component in root.GetComponentsInChildren<NetworkObject>(true))
                { component.SetIsNetworked(false); component.gameObject.SetActive(true); }
            // Network wrappers no longer block local pickup callbacks after their destruction.
            await Task.Yield();
            GameObject player = Instantiate(offlinePlayerPrefab, new Vector3(0f, .05f, -3f), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(player, scene);
            player.GetComponent<WorldOutfitRenderer>()?.SetFirstPersonHidden(true);
            Camera.main.GetComponent<GrayboxFirstPersonCamera>().SetTarget(player.transform);
            FindAnyObjectByType<MannequinWardrobe>()?.Bind(player.GetComponent<PlayerOutfit>());
            MenuVisible = false; Report("Одиночная игра. Esc — меню.");
        }
        catch (Exception error) { await StopSession(); Report(error.Message); }
        finally { Busy = false; Changed?.Invoke(); }
    }
}
