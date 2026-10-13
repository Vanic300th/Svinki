using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

[Serializable] public struct LobbyMediaState
{
    public string Url, Title, AddedBy, PlayerId;
    public int Version;
    public bool Playing;
    public double StartedAt, Position, Duration;
}

public abstract class LobbyMediaPlayer : LobbyInteractable
{
    [SerializeField] protected AudioSource speaker;
    [SerializeField, Range(5, 60)] private float loadTimeout = 20;
    [SerializeField, Range(.2f, 3)] private float driftTolerance = .65f;
    [SerializeField, Range(1, 10)] private float correctionInterval = 3;
    [SerializeField, Min(1)] private float commandCooldown = .6f;
    [SerializeField, Min(1)] private int maxAudioMegabytes = 32;
    protected readonly SyncVar<LobbyMediaState> state = new SyncVar<LobbyMediaState>();
    protected LobbyMediaState offlineState;
    private readonly Dictionary<string, float> nextControl = new Dictionary<string, float>();
    protected VideoPlayer video;
    private Coroutine loading;
    private int appliedVersion = -1;
    private float nextCorrection;
    private AudioClip audioClip;
    private bool audioStarted;
    public abstract LobbyMediaKind Kind { get; }
    public LobbyMediaState State => NetworkLobby.Instance?.Offline == true ? offlineState : state.Value;
    public string LocalError { get; protected set; }
    public CatalogTrack LocalCatalogTrack { get; private set; }
    public virtual Texture LocalPreview => video != null ? video.targetTexture : null;
    public double Clock => NetworkObject != null && IsSpawned ? TimeManager.TicksToTime(TimeManager.Tick) : Time.unscaledTimeAsDouble;
    public double Position => State.Position + (State.Playing ? Math.Max(0, Clock - State.StartedAt) : 0);
    private string VolumeKey => "svinki.lobby." + Kind + ".volume";
    private float localVolume;
    public float LocalVolume { get => localVolume; set { localVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(VolumeKey, localVolume); ApplyMusicVolume(); } }
    private void ApplyMusicVolume() => speaker.volume = localVolume * (Kind == LobbyMediaKind.Audio ? MusicSettings.Volume : 1);
    protected virtual void Awake()
    {
        speaker.spatialBlend = 1; speaker.minDistance = 2; speaker.maxDistance = 22; speaker.rolloffMode = AudioRolloffMode.Logarithmic;
        speaker.playOnAwake = false; localVolume = PlayerPrefs.GetFloat(VolumeKey, .35f); ApplyMusicVolume();
        video = GetComponent<VideoPlayer>();
        if (video != null) { video.playOnAwake = false; video.errorReceived += VideoFailed; video.loopPointReached += VideoEnded; }
    }
    public override string Prompt(PlayerAvatar player) => "E — " + (Kind == LobbyMediaKind.Video ? "TV · YouTube / video URL" : "jukebox · music search / audio URL");
    public override void Interact(PlayerAvatar player) => LobbyPanel.Open(this, player);
    protected void SetState(LobbyMediaState next) { if (NetworkLobby.Instance?.Offline == true) offlineState = next; else state.Value = next; }
    protected void Load(ResolvedMedia media, string player, string id)
    {
        SetState(new LobbyMediaState { Url = media.Url, Title = media.Title, PlayerId = id, AddedBy = player, Version = State.Version + 1,
            Playing = true, StartedAt = Clock });
    }
    protected bool AllowControl(PlayerAvatar player)
    {
        string id = Identity(player);
        if (nextControl.TryGetValue(id, out float deadline) && Time.unscaledTime < deadline) return false;
        nextControl[id] = Time.unscaledTime + commandCooldown; return true;
    }
    public override void Apply(PlayerAvatar player, LobbyAction action, string text, int index, Vector2[] points)
    {
        if (!AllowControl(player)) return;
        var current = State;
        if (action == LobbyAction.Load)
        {
            if (MediaSourceProvider.TryResolve(text, Kind, out var media)) Load(media, Nickname(player), Identity(player));
        }
        else if (action == LobbyAction.PlayPause && !string.IsNullOrEmpty(current.Url))
        { current.Position = Position; current.StartedAt = Clock; current.Playing = !current.Playing; SetState(current); }
        else if (action == LobbyAction.Seek && current.Duration > 0 && index >= 0 && index <= current.Duration)
        { current.Position = index; current.StartedAt = Clock; SetState(current); }
        else if (action == LobbyAction.Stop) StopMedia();
    }
    protected void StopMedia() => SetState(new LobbyMediaState { Version = State.Version + 1 });
    protected virtual void Finished() { if (Authority) StopMedia(); }
    private bool CurrentVideo(VideoPlayer player) => MediaSourceProvider.TryResolve(State.Url, Kind, out var media) && media.Backend == LobbyMediaBackend.Direct && player.url == media.Url;
    private void VideoFailed(VideoPlayer player, string error) { if (!CurrentVideo(player)) return; LocalError = "Video could not load: " + error; if (Authority) Finished(); }
    private void VideoEnded(VideoPlayer player) { if (CurrentVideo(player) && Authority) Finished(); }
    public override void LobbyTick(float delta)
    {
        ApplyMusicVolume();
        if (ExternalTick(delta)) return;
        if (State.Version != appliedVersion)
        {
            ResetLocalPlayback();
            loading = !string.IsNullOrEmpty(State.Url) ? StartCoroutine(Prepare(State.Version)) : null;
        }
        if (loading != null || string.IsNullOrEmpty(State.Url)) return;
        bool prepared = video != null ? video.isPrepared : speaker.clip != null;
        if (!prepared) return;
        double duration = video != null ? video.length : speaker.clip.length;
        if (Authority && State.Duration == 0 && double.IsFinite(duration) && duration > 0) { var current = State; current.Duration = duration; SetState(current); }
        if (Authority && State.Playing && duration > 0 && Position >= duration) { Finished(); return; }
        double desired = Math.Max(0, Math.Min(Position, Math.Max(0, duration - .05)));
        double actual = video != null ? video.time : speaker.time;
        bool playing = video != null ? video.isPlaying : speaker.isPlaying;
        if (Time.unscaledTime >= nextCorrection || State.Playing != playing)
        {
            nextCorrection = Time.unscaledTime + correctionInterval;
            if (Math.Abs(actual - desired) > driftTolerance)
            {
                if (video != null && video.canSetTime) video.time = desired;
                else if (video == null) speaker.time = (float)desired;
            }
        }
        if (video != null) { if (State.Playing && !video.isPlaying) video.Play(); else if (!State.Playing && video.isPlaying) video.Pause(); }
        else
        {
            if (State.Playing && !speaker.isPlaying)
            {
                if (!audioStarted) { speaker.Play(); speaker.time = (float)desired; audioStarted = true; }
                else { speaker.UnPause(); speaker.time = (float)desired; }
            }
            else if (!State.Playing && speaker.isPlaying) speaker.Pause();
        }
    }
    protected void ResetLocalPlayback()
    {
        appliedVersion = State.Version; if (!string.IsNullOrEmpty(State.Url)) LocalError = null; audioStarted = false; LocalCatalogTrack = null;
        if (loading != null) StopCoroutine(loading); loading = null;
        if (video != null) video.Stop(); speaker.Stop();
        if (audioClip != null) Destroy(audioClip); audioClip = null; speaker.clip = null;
    }
    protected virtual bool ExternalTick(float delta) => false;
    protected void SetDuration(double duration)
    { if (Authority && double.IsFinite(duration) && duration > 0 && duration <= 86400 && State.Duration != duration) { var current = State; current.Duration = duration; SetState(current); } }
    private IEnumerator Prepare(int version)
    {
        yield return null;
        if (!MediaSourceProvider.TryResolve(State.Url, Kind, out var media)) { LocalError = "Unsupported direct media URL"; loading = null; yield break; }
        if (media.Backend == LobbyMediaBackend.CcMixter)
        {
            CatalogTrack track = null; string error = null;
            yield return new CcMixterMusicCatalog().Resolve(media, (t, e) => { track = t; error = e; });
            if (State.Version != version) yield break;
            if (track == null) { LocalError = error; loading = null; if (Authority) Finished(); yield break; }
            LocalCatalogTrack = track; media.Url = track.StreamUrl;
        }
        if (video != null)
        {
            video.source = VideoSource.Url; video.url = media.Url; video.Prepare();
            float deadline = Time.unscaledTime + loadTimeout;
            while (!video.isPrepared && LocalError == null && Time.unscaledTime < deadline && State.Version == version) yield return null;
            if (!video.isPrepared && State.Version == version) { LocalError = "Video timed out or the codec is unsupported"; if (Authority) Finished(); }
        }
        else
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip(media.Url, media.AudioType))
            {
                request.timeout = Mathf.CeilToInt(loadTimeout);
                // ccMixter's public file server requires the validated source page as Referer.
                // This comes from the official API, never an arbitrary client-supplied header.
                if (LocalCatalogTrack != null) request.SetRequestHeader("Referer", LocalCatalogTrack.Source);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (request.downloadedBytes > (ulong)maxAudioMegabytes * 1024 * 1024) { request.Abort(); break; }
                    yield return null;
                }
                if (State.Version != version) { loading = null; yield break; }
                if (request.result != UnityWebRequest.Result.Success || request.downloadedBytes > (ulong)maxAudioMegabytes * 1024 * 1024)
                { LocalError = request.downloadedBytes > (ulong)maxAudioMegabytes * 1024 * 1024 ? "Audio exceeds size limit" : "Audio could not load (HTTP " + request.responseCode + "): " + request.error; if (Authority) Finished(); }
                else { audioClip = DownloadHandlerAudioClip.GetContent(request); speaker.clip = audioClip; }
            }
        }
        loading = null; nextCorrection = 0;
    }
    protected virtual void OnDestroy()
    {
        if (video != null) { video.errorReceived -= VideoFailed; video.loopPointReached -= VideoEnded; }
        if (audioClip != null) Destroy(audioClip);
    }
}
