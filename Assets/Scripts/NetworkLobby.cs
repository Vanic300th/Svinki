using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    private string address = "";
    private string portText = "7770";
    private string connectedAddress = "";
    private ushort connectedPort;
    private string lanAddresses = "";
    private bool hostingLocally;
    private GUIStyle wrappedLabel;
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
            status = hostingLocally
                ? "Локальный сервер запущен. Создайте комнату и передайте другу IP этого компьютера."
                : "Подключено к " + connectedAddress + ":" + connectedPort + ". Создайте комнату или введите код.";
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
        if (!reply.Accepted && reply.Message == "Комната с таким кодом не найдена.")
            status += " Проверьте, что адрес сервера — IP хозяина, а не 127.0.0.1.";
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

    private static string FindLanAddresses()
    {
        var addresses = new HashSet<string>();
        try
        {
            foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.OperationalStatus != OperationalStatus.Up ||
                    network.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    network.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                foreach (UnicastIPAddressInformation entry in network.GetIPProperties().UnicastAddresses)
                {
                    IPAddress ip = entry.Address;
                    if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)) continue;
                    byte[] octets = ip.GetAddressBytes();
                    bool privateAddress = octets[0] == 10 ||
                        (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31) ||
                        (octets[0] == 192 && octets[1] == 168);
                    if (privateAddress) addresses.Add(ip.ToString());
                }
            }
        }
        catch (NetworkInformationException) { }
        catch (PlatformNotSupportedException) { }
        return addresses.Count == 0 ? "IP не найден — посмотрите IPv4 в настройках Wi-Fi" : string.Join(", ", addresses);
    }

    private void StartLocalServer(ushort port)
    {
        if (!manager.ServerManager.StartConnection(port))
        {
            status = "Не удалось запустить сервер на UDP " + port + ". Проверьте, не занят ли порт.";
            return;
        }
        hostingLocally = true;
        lanAddresses = FindLanAddresses();
        connectedAddress = "127.0.0.1";
        connectedPort = port;
        if (!manager.ClientManager.StartConnection(connectedAddress, port))
        {
            manager.ServerManager.StopConnection(true);
            hostingLocally = false;
            status = "Сервер запущен, но локальный клиент не смог подключиться.";
            return;
        }
        status = "Сервер запущен. Другу нужен ваш IP: " + lanAddresses + ", UDP " + port + ".";
    }

    private void OnGUI()
    {
        if (Application.isBatchMode) return;
        if (playing)
        {
            GUI.Box(new Rect(12, 50, 190, 32), "Комната: " + code);
            if (hostingLocally)
                GUI.Box(new Rect(12, 88, 340, 48), "Для друга — IP: " + lanAddresses + "\nUDP порт: " + connectedPort);
            return;
        }

        float width = 380f;
        if (wrappedLabel == null) wrappedLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
        GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 70f, width, 390f), GUI.skin.box);
        GUILayout.Label("SVINKI — мультиплеер");
        GUILayout.Label(status, wrappedLabel, GUILayout.Height(44f));
        if (!manager.ClientManager.Started)
        {
            GUILayout.Label("Адрес сервера (IP хозяина в Wi-Fi)");
            address = GUILayout.TextField(address);
            if (address.Trim() == "127.0.0.1" || address.Trim().Equals("localhost", StringComparison.OrdinalIgnoreCase))
                GUILayout.Label("127.0.0.1 — это этот компьютер, не компьютер друга.", wrappedLabel);
            GUILayout.Label("UDP порт");
            portText = GUILayout.TextField(portText);
            if (GUILayout.Button("Подключиться к серверу"))
            {
                if (string.IsNullOrWhiteSpace(address))
                    status = "Введите IP компьютера хозяина. Код комнаты вводится после подключения.";
                else if (address.Trim() == "127.0.0.1" ||
                         address.Trim().Equals("localhost", StringComparison.OrdinalIgnoreCase))
                    status = "127.0.0.1 ведёт на ваш компьютер. Введите IP компьютера хозяина.";
                else if (!ushort.TryParse(portText, out ushort port))
                    status = "Введите верный UDP порт.";
                else
                {
                    connectedAddress = address.Trim();
                    connectedPort = port;
                    status = "Подключаемся к " + connectedAddress + ":" + port + "...";
                    if (!manager.ClientManager.StartConnection(connectedAddress, port))
                        status = "Не удалось подключиться к " + connectedAddress + ":" + port + ".";
                }
            }
            if (!manager.ServerManager.Started && GUILayout.Button("Создать сервер в локальной сети"))
            {
                if (ushort.TryParse(portText, out ushort localPort)) StartLocalServer(localPort);
                else status = "Введите верный UDP порт.";
            }
        }
        else if (!joining)
        {
            if (hostingLocally)
                GUILayout.Label("IP для друга: " + lanAddresses + " · UDP " + connectedPort, wrappedLabel);
            if (GUILayout.Button("Создать комнату"))
                manager.ClientManager.Broadcast(new RoomRequest { Create = true });
            GUILayout.Label("Код комнаты");
            code = GUILayout.TextField(code, 6).ToUpperInvariant();
            if (GUILayout.Button("Войти по коду"))
                manager.ClientManager.Broadcast(new RoomRequest { Create = false, Code = code });
            if (GUILayout.Button("Отключиться"))
            {
                manager.ClientManager.StopConnection();
                if (hostingLocally) manager.ServerManager.StopConnection(true);
                hostingLocally = false;
            }
        }
        else GUILayout.Label("Загружаем уровень...");
        GUILayout.EndArea();
    }
}
