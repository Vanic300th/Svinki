using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager))]
public sealed class NetworkLobby : MonoBehaviour
{
    private struct RoomRequest : IBroadcast { public bool Create; public string Code; }
    private struct RoomReply : IBroadcast { public bool Accepted; public string Code; public string Message; }

    private sealed class Room
    {
        public string Code;
        public Scene Scene;
        public readonly HashSet<NetworkConnection> Members = new HashSet<NetworkConnection>();
        public readonly List<NetworkConnection> Waiting = new List<NetworkConnection>();
    }

    public static NetworkLobby Instance { get; private set; }
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField, Range(2, 4)] private int maxPlayersPerRoom = 4;
    [SerializeField] private ushort defaultPort = 7770;

    private readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();
    private readonly Dictionary<NetworkConnection, Room> memberships = new Dictionary<NetworkConnection, Room>();
    private NetworkManager manager;
    private string address = "127.0.0.1";
    private string portText = "7770";
    private string code = "";
    private string status = "Подключитесь к серверу";
    private bool playing;
    private bool joining;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        manager = GetComponent<NetworkManager>();
        portText = defaultPort.ToString();
        manager.ServerManager.RegisterBroadcast<RoomRequest>(OnRoomRequest);
        manager.ClientManager.RegisterBroadcast<RoomReply>(OnRoomReply);
        manager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        manager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        manager.SceneManager.OnLoadEnd += OnLoadEnd;
        manager.SceneManager.OnClientPresenceChangeEnd += OnPresenceChanged;
    }

    private void Start()
    {
        if (!Application.isBatchMode) return;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--port" && ushort.TryParse(args[i + 1], out ushort parsed))
                defaultPort = parsed;
        manager.ServerManager.StartConnection(defaultPort);
        Debug.Log($"Svinki server listening on UDP {defaultPort}");
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (manager == null) return;
        manager.ServerManager.UnregisterBroadcast<RoomRequest>(OnRoomRequest);
        manager.ClientManager.UnregisterBroadcast<RoomReply>(OnRoomReply);
        manager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        manager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        manager.SceneManager.OnLoadEnd -= OnLoadEnd;
        manager.SceneManager.OnClientPresenceChangeEnd -= OnPresenceChanged;
    }

    public void SetPlaying(bool value) => playing = value;

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
            status = "Подключено. Создайте комнату или введите код.";
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            playing = false;
            joining = false;
            code = "";
            status = "Соединение закрыто";
        }
    }

    private void OnRoomReply(RoomReply reply, Channel _)
    {
        joining = reply.Accepted;
        if (reply.Accepted) code = reply.Code;
        status = reply.Message;
    }

    private void OnRoomRequest(NetworkConnection connection, RoomRequest request, Channel _)
    {
        if (connection == null || memberships.ContainsKey(connection))
        {
            Reply(connection, false, "", "Вы уже состоите в комнате.");
            return;
        }

        Room room;
        if (request.Create)
        {
            string newCode;
            do { newCode = GenerateCode(); } while (rooms.ContainsKey(newCode));
            room = new Room { Code = newCode };
            rooms.Add(newCode, room);
            room.Members.Add(connection);
            memberships.Add(connection, room);
            Reply(connection, true, newCode, "Комната " + newCode + " создана. Загрузка уровня...");
            SceneLoadData data = new SceneLoadData("SampleScene");
            data.Options.AllowStacking = true;
            data.Options.LocalPhysics = LocalPhysicsMode.Physics3D;
            data.Params.ServerParams = new object[] { newCode };
            manager.SceneManager.LoadConnectionScenes(connection, data);
            return;
        }

        string requestedCode = (request.Code ?? "").Trim().ToUpperInvariant();
        if (!rooms.TryGetValue(requestedCode, out room))
        {
            Reply(connection, false, "", "Комната с таким кодом не найдена.");
            return;
        }
        if (room.Members.Count >= maxPlayersPerRoom)
        {
            Reply(connection, false, "", "Комната заполнена.");
            return;
        }
        room.Members.Add(connection);
        memberships.Add(connection, room);
        Reply(connection, true, room.Code, "Вход в комнату " + room.Code + "...");
        if (room.Scene.IsValid())
            manager.SceneManager.LoadConnectionScenes(connection, new SceneLoadData(room.Scene));
        else room.Waiting.Add(connection);
    }

    private void Reply(NetworkConnection connection, bool accepted, string roomCode, string message)
    {
        if (connection != null && connection.IsActive)
            manager.ServerManager.Broadcast(connection, new RoomReply
                { Accepted = accepted, Code = roomCode, Message = message });
    }

    private void OnLoadEnd(SceneLoadEndEventArgs args)
    {
        if (!args.QueueData.AsServer || args.LoadedScenes.Length == 0) return;
        object[] parameters = args.QueueData.SceneLoadData.Params.ServerParams;
        if (parameters == null || parameters.Length == 0 || !(parameters[0] is string roomCode)) return;
        if (!rooms.TryGetValue(roomCode, out Room room)) return;
        room.Scene = args.LoadedScenes[0];
        foreach (NetworkConnection waiting in room.Waiting)
            if (waiting.IsActive)
                manager.SceneManager.LoadConnectionScenes(waiting, new SceneLoadData(room.Scene));
        room.Waiting.Clear();
    }

    private void OnPresenceChanged(ClientPresenceChangeEventArgs args)
    {
        if (!args.Added || !memberships.TryGetValue(args.Connection, out Room room) ||
            room.Scene != args.Scene || playerPrefab == null || args.Connection.FirstObject != null) return;
        int index = 0;
        foreach (NetworkConnection member in room.Members)
        {
            if (member == args.Connection) break;
            index++;
        }
        Vector3 spawnPosition = new Vector3((index % 2) * 1.5f, 0.05f, -3f + (index / 2) * 1.5f);
        NetworkObject player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        manager.ServerManager.Spawn(player, args.Connection, room.Scene);
        Reply(args.Connection, true, room.Code, "Комната " + room.Code + " — игроков: " + room.Members.Count);
    }

    private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Stopped || !memberships.TryGetValue(connection, out Room room)) return;
        memberships.Remove(connection);
        room.Members.Remove(connection);
        room.Waiting.Remove(connection);
        if (room.Members.Count == 0)
        {
            rooms.Remove(room.Code);
            if (room.Scene.IsValid()) manager.SceneManager.UnloadConnectionScenes(new SceneUnloadData(room.Scene));
        }
    }

    private static string GenerateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] chars = new char[6];
        for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[UnityEngine.Random.Range(0, alphabet.Length)];
        return new string(chars);
    }

    private void OnGUI()
    {
        if (Application.isBatchMode) return;
        if (playing)
        {
            GUI.Box(new Rect(12, 12, 190, 32), "Комната: " + code);
            return;
        }

        float width = 380f;
        GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 70f, width, 330f), GUI.skin.box);
        GUILayout.Label("SVINKI — мультиплеер");
        GUILayout.Label(status);
        if (!manager.ClientManager.Started)
        {
            GUILayout.Label("Адрес сервера");
            address = GUILayout.TextField(address);
            GUILayout.Label("UDP порт");
            portText = GUILayout.TextField(portText);
            if (GUILayout.Button("Подключиться") && ushort.TryParse(portText, out ushort port))
                manager.ClientManager.StartConnection(address.Trim(), port);
            if (Application.isEditor && !manager.ServerManager.Started && GUILayout.Button("Локальный сервер для проверки"))
            {
                if (ushort.TryParse(portText, out ushort localPort))
                {
                    manager.ServerManager.StartConnection(localPort);
                    manager.ClientManager.StartConnection("127.0.0.1", localPort);
                }
            }
        }
        else if (!joining)
        {
            if (GUILayout.Button("Создать комнату"))
                manager.ClientManager.Broadcast(new RoomRequest { Create = true });
            GUILayout.Label("Код комнаты");
            code = GUILayout.TextField(code, 6).ToUpperInvariant();
            if (GUILayout.Button("Войти по коду"))
                manager.ClientManager.Broadcast(new RoomRequest { Create = false, Code = code });
            if (GUILayout.Button("Отключиться")) manager.ClientManager.StopConnection();
        }
        else GUILayout.Label("Загружаем уровень...");
        GUILayout.EndArea();
    }
}
