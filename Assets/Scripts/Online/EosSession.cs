using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
#if !UNITY_WEBGL && !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.P2P;
using PlayEveryWare.EpicOnlineServices;
#endif

public sealed class EosSession : MonoBehaviour
{
    [Serializable] public sealed class Settings
    {
        public string productId, sandboxId, deploymentId, clientId, clientSecret;
    }
    public string Identity { get; private set; }
    public string Code { get; private set; }
    public string HostIdentity { get; private set; }
    public bool Hosting { get; private set; }
    public event Action<string> SessionClosed;
#if !UNITY_WEBGL && !EOS_DISABLE
    private PlatformInterface platform;
    private ProductUserId user;
    private ulong memberNotify, authNotify;
    // Read only in player builds (#if !UNITY_EDITOR in OnDestroy), so the editor compiler reports them as unused.
#pragma warning disable 0414
    private bool shuttingDown, applicationQuitting;
    private static bool ownsSdkInitialization;
#pragma warning restore 0414
    private LobbyInterface Lobby => platform.GetLobbyInterface();
    public async Task Authenticate(string nickname)
    {
        if (user != null) return;
        if (platform == null)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "svinki-eos.json");
            if (!File.Exists(path)) throw new InvalidOperationException("Online play is not configured: svinki-eos.json is missing. See MULTIPLAYER.md.");
            Settings settings = JsonUtility.FromJson<Settings>(File.ReadAllText(path));
            if (settings == null || string.IsNullOrEmpty(settings.productId) || string.IsNullOrEmpty(settings.sandboxId) ||
                string.IsNullOrEmpty(settings.deploymentId) || string.IsNullOrEmpty(settings.clientId) || string.IsNullOrEmpty(settings.clientSecret))
                throw new InvalidOperationException("Fill in your EOS project settings in svinki-eos.json.");
#if UNITY_EDITOR_OSX
            // The SDK selects the Windows binary when a Windows build target is active on macOS.
            if (Common.LIBRARY_NAME != "libEOSSDK-Mac-Shipping")
                throw new InvalidOperationException("To test EOS in the Editor on Mac, select macOS in File > Build Profiles. Test Windows builds on Windows.");
#endif
            EOSManager.EOSSingleton.LoadEOSLibraries();
            var init = new InitializeOptions { ProductName = "Svinki", ProductVersion = "1.0" };
            Result result = PlatformInterface.Initialize(ref init);
            if (result != Result.Success && result != Result.AlreadyConfigured) Check(result, "Initializing EOS");
            if (result == Result.Success) ownsSdkInitialization = true;
            var options = new Options
            {
                ProductId = settings.productId, SandboxId = settings.sandboxId, DeploymentId = settings.deploymentId,
                ClientCredentials = new ClientCredentials { ClientId = settings.clientId, ClientSecret = settings.clientSecret },
                IsServer = false, Flags = PlatformFlags.DisableOverlay,
                CacheDirectory = Path.Combine(Application.persistentDataPath, "eos-cache"), TickBudgetInMilliseconds = 4
            };
            platform = PlatformInterface.Create(ref options);
            if (platform == null) throw new InvalidOperationException("Could not create the EOS platform. Check your project settings.");
            FishNet.Plugins.FishyEOS.Util.EOS.Platform = platform;
            var relay = new SetRelayControlOptions { RelayControl = RelayControl.AllowRelays };
            Check(platform.GetP2PInterface().SetRelayControl(ref relay), "Enabling relay");
        }
        var deviceTask = new TaskCompletionSource<Result>();
        var device = new CreateDeviceIdOptions { DeviceModel = SystemInfo.operatingSystemFamily.ToString() };
        platform.GetConnectInterface().CreateDeviceId(ref device, null, (ref CreateDeviceIdCallbackInfo info) => deviceTask.TrySetResult(info.ResultCode));
        Result created = await WithTimeout(deviceTask.Task);
        if (created != Result.Success && created != Result.DuplicateNotAllowed) Check(created, "Creating guest profile");
        await Login(nickname);
        var authOptions = new AddNotifyAuthExpirationOptions();
        authNotify = platform.GetConnectInterface().AddNotifyAuthExpiration(ref authOptions, null,
            (ref AuthExpirationCallbackInfo info) => RefreshLogin(nickname));
        var memberOptions = new AddNotifyLobbyMemberStatusReceivedOptions();
        memberNotify = Lobby.AddNotifyLobbyMemberStatusReceived(ref memberOptions, null, OnMemberStatus);
    }
    private async Task Login(string nickname)
    {
        var completion = new TaskCompletionSource<LoginCallbackInfo>();
        var login = new LoginOptions
        {
            Credentials = new Credentials { Type = ExternalCredentialType.DeviceidAccessToken },
            UserLoginInfo = new UserLoginInfo { DisplayName = nickname }
        };
        platform.GetConnectInterface().Login(ref login, null, (ref LoginCallbackInfo info) => completion.TrySetResult(info));
        LoginCallbackInfo answer = await WithTimeout(completion.Task);
        if (answer.ResultCode == Result.InvalidUser)
        {
            var createTask = new TaskCompletionSource<CreateUserCallbackInfo>();
            var create = new CreateUserOptions { ContinuanceToken = answer.ContinuanceToken };
            platform.GetConnectInterface().CreateUser(ref create, null, (ref CreateUserCallbackInfo info) => createTask.TrySetResult(info));
            CreateUserCallbackInfo newUser = await WithTimeout(createTask.Task);
            Check(newUser.ResultCode, "Creating profile"); user = newUser.LocalUserId;
        }
        else { Check(answer.ResultCode, "Guest sign-in"); user = answer.LocalUserId; }
        Identity = user.ToString();
    }
    private async void RefreshLogin(string nickname)
    {
        try { await Login(nickname); }
        catch (Exception) { SessionClosed?.Invoke("The guest profile lost its EOS connection."); }
    }
    public async Task Create()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string candidate = GenerateCode();
            var completion = new TaskCompletionSource<CreateLobbyCallbackInfo>();
            var options = new CreateLobbyOptions
            {
                LocalUserId = user, MaxLobbyMembers = 6, LobbyId = candidate, BucketId = "svinki-v1",
                PermissionLevel = LobbyPermissionLevel.Publicadvertised, DisableHostMigration = true,
                EnableJoinById = true, AllowInvites = false, RejoinAfterKickRequiresInvite = true
            };
            bool abandoned = false;
            Lobby.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo info) =>
            {
                if (abandoned && info.ResultCode == Result.Success)
                {
                    var destroy = new DestroyLobbyOptions { LocalUserId = user, LobbyId = info.LobbyId };
                    Lobby.DestroyLobby(ref destroy, null, (ref DestroyLobbyCallbackInfo answer) => { });
                }
                completion.TrySetResult(info);
            });
            CreateLobbyCallbackInfo reply;
            try { reply = await WithTimeout(completion.Task); }
            catch { abandoned = true; throw; }
            if (reply.ResultCode == Result.LobbyLobbyAlreadyExists) continue;
            Check(reply.ResultCode, "Creating lobby");
            Code = reply.LobbyId.ToString(); HostIdentity = Identity; Hosting = true; return;
        }
        throw new InvalidOperationException("Could not find an available code. Try again.");
    }
    public async Task Join(string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length != 6) throw new InvalidOperationException("Enter the six-digit lobby code.");
        // Search verifies the product bucket before joining a lobby with a globally unique override ID.
        var searchOptions = new CreateLobbySearchOptions { MaxResults = 1 };
        Check(Lobby.CreateLobbySearch(ref searchOptions, out LobbySearch search), "Finding lobby");
        try
        {
            var id = new LobbySearchSetLobbyIdOptions { LobbyId = code };
            Check(search.SetLobbyId(ref id), "Searching by code");
            var find = new LobbySearchFindOptions { LocalUserId = user };
            var found = new TaskCompletionSource<Result>();
            search.Find(ref find, null, (ref LobbySearchFindCallbackInfo info) => found.TrySetResult(info.ResultCode));
            Check(await WithTimeout(found.Task), "Finding lobby");
            var copy = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = 0 };
            Result result = search.CopySearchResultByIndex(ref copy, out LobbyDetails details);
            if (result != Result.Success) throw new InvalidOperationException("The lobby was not found or is already closed.");
            try
            {
                var infoOptions = new LobbyDetailsCopyInfoOptions();
                Check(details.CopyInfo(ref infoOptions, out LobbyDetailsInfo? info), "Reading lobby");
                if (info == null || info.Value.BucketId.ToString() != "svinki-v1") throw new InvalidOperationException("Incompatible game version.");
                HostIdentity = info.Value.LobbyOwnerUserId.ToString();
                var join = new JoinLobbyOptions { LocalUserId = user, LobbyDetailsHandle = details };
                var joined = new TaskCompletionSource<Result>();
                bool abandoned = false;
                Lobby.JoinLobby(ref join, null, (ref JoinLobbyCallbackInfo answer) =>
                {
                    if (abandoned && answer.ResultCode == Result.Success && Code != code)
                    {
                        var leave = new LeaveLobbyOptions { LocalUserId = user, LobbyId = code };
                        Lobby.LeaveLobby(ref leave, null, (ref LeaveLobbyCallbackInfo left) => { });
                    }
                    joined.TrySetResult(answer.ResultCode);
                });
                Result joinedResult;
                try { joinedResult = await WithTimeout(joined.Task); }
                catch { abandoned = true; throw; }
                if (joinedResult != Result.NoChange) Check(joinedResult, "Joining lobby");
                Code = code; Hosting = false;
            }
            finally { details.Release(); }
        }
        finally { search.Release(); }
    }
    public async Task Leave()
    {
        if (platform == null || string.IsNullOrEmpty(Code)) return;
        string leaving = Code; Code = null;
        var done = new TaskCompletionSource<Result>();
        if (Hosting)
        {
            var options = new DestroyLobbyOptions { LocalUserId = user, LobbyId = leaving };
            Lobby.DestroyLobby(ref options, null, (ref DestroyLobbyCallbackInfo info) => done.TrySetResult(info.ResultCode));
        }
        else
        {
            var options = new LeaveLobbyOptions { LocalUserId = user, LobbyId = leaving };
            Lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo info) => done.TrySetResult(info.ResultCode));
        }
        Hosting = false;
        try { await WithTimeout(done.Task); } catch (TimeoutException) { }
    }
    public void Kick(string identity)
    {
        if (!Hosting) return;
        var options = new KickMemberOptions { LocalUserId = user, LobbyId = Code, TargetUserId = ProductUserId.FromString(identity) };
        Lobby.KickMember(ref options, null, (ref KickMemberCallbackInfo info) => { });
    }
    private void OnMemberStatus(ref LobbyMemberStatusReceivedCallbackInfo info)
    {
        if (info.LobbyId.ToString() != Code || Hosting) return;
        if (info.CurrentStatus == LobbyMemberStatus.Closed || info.CurrentStatus == LobbyMemberStatus.Kicked && info.TargetUserId == user ||
            info.TargetUserId?.ToString() == HostIdentity && info.CurrentStatus != LobbyMemberStatus.Joined)
            SessionClosed?.Invoke("The host ended the session or removed you. The host keeps the checkpoint.");
    }
    private void Update() { if (!shuttingDown) platform?.Tick(); }
    private void OnApplicationQuit() { applicationQuitting = true; }
    private void OnDestroy()
    {
        shuttingDown = true;
        if (platform != null)
        {
            GetComponent<FishNet.Managing.NetworkManager>()?.TransportManager?.Transport?.Shutdown();
            if (memberNotify != 0) Lobby.RemoveNotifyLobbyMemberStatusReceived(memberNotify);
            if (authNotify != 0) platform.GetConnectInterface().RemoveNotifyAuthExpiration(authNotify);
            FishNet.Plugins.FishyEOS.Util.EOS.Platform = null;
            platform.Release(); platform = null;
        }
        // EOS_Initialize is once per process. Shutdown makes all later SDK calls invalid.
        // Stopping Play or replacing this component releases only its platform instance.
#if !UNITY_EDITOR
        if (applicationQuitting && ownsSdkInitialization)
        {
            PlatformInterface.Shutdown(); ownsSdkInitialization = false;
        }
#endif
    }
    private static void Check(Result result, string operation)
    {
        if (result != Result.Success) throw new InvalidOperationException(operation + ": " + result + ". Check your connection and EOS settings.");
    }
    private static async Task<T> WithTimeout<T>(Task<T> task)
    {
        if (await Task.WhenAny(task, Task.Delay(25000)) != task) throw new TimeoutException("EOS did not respond within 25 seconds. Check your connection and try again.");
        return await task;
    }
#else
    public Task Authenticate(string nickname) => Task.FromException(new InvalidOperationException("Online play is only available in the desktop version."));
    public Task Create() => Authenticate("");
    public Task Join(string code) => Authenticate("");
    public Task Leave() => Task.CompletedTask;
    public void Kick(string identity) { }
#endif
    public static string GenerateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[6];
        using (var random = System.Security.Cryptography.RandomNumberGenerator.Create()) random.GetBytes(bytes);
        var code = new char[6]; for (int i = 0; i < code.Length; i++) code[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(code);
    }
}
