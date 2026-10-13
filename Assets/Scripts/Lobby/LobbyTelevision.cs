using System.Linq;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer), typeof(AudioSource))]
public sealed class LobbyTelevision : LobbyMediaPlayer
{
    [SerializeField] private Renderer screen;
    private RenderTexture target;
    private Material screenMaterial;
    private YouTubePlayback youtube;
    private int youtubeVersion = -1;
    private float nextBrowserFrame;
    public bool UsesYouTube => youtube != null;
    public override Texture LocalPreview => target;
    public override LobbyMediaKind Kind => LobbyMediaKind.Video;
    protected override void Awake()
    {
        base.Awake();
        target = new RenderTexture(640, 360, 0); target.Create();
        video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = target;
        video.audioOutputMode = VideoAudioOutputMode.AudioSource; video.SetTargetAudioSource(0, speaker);
        screenMaterial = screen.material; screenMaterial.mainTexture = target; screenMaterial.color = Color.white;
    }
    protected override void OnDestroy()
    {
        youtube?.Dispose(); youtube = null;
        base.OnDestroy(); if (target != null) { target.Release(); Destroy(target); } if (screenMaterial != null) Destroy(screenMaterial);
    }
    protected override bool ExternalTick(float delta)
    {
        bool external = MediaSourceProvider.TryResolve(State.Url, Kind, out var media) && media.Backend == LobbyMediaBackend.YouTube;
        if (!external) { if (youtube != null) { youtube.Dispose(); youtube = null; youtubeVersion = -1; } return false; }
        if (youtube == null || youtubeVersion != State.Version)
        {
            youtube?.Dispose(); ResetLocalPlayback(); youtubeVersion = State.Version;
            youtube = new YouTubePlayback(media.Id, State.Version); LocalError = null;
        }
        var camera = PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal)?.EyeCamera;
        bool visible = camera != null && (LobbyPanel.IsViewing(this) || VisibleTo(camera));
        float distance = camera != null ? Vector3.Distance(camera.transform.position, transform.position) : speaker.maxDistance;
        float attenuation = distance >= speaker.maxDistance ? 0 : Mathf.Clamp01(speaker.minDistance / Mathf.Max(speaker.minDistance, distance));
        float volume = speaker.mute || AudioListener.pause ? 0 : LocalVolume * attenuation * AudioListener.volume;
        youtube.Tick(Position, State.Playing, volume, visible); LocalError = youtube.Error;
        SetDuration(youtube.Duration);
        var local = PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal);
        if (visible && local != null && (youtube.NativeIntent == 1 && !State.Playing || youtube.NativeIntent == 2 && State.Playing)) Request(local, LobbyAction.PlayPause);
        if (visible && local != null && youtube.NativeIntent == 3 && State.Duration > 0 && System.Math.Abs(youtube.NativePosition - Position) > 2) Request(local, LobbyAction.Seek, index: Mathf.Clamp(Mathf.RoundToInt((float)youtube.NativePosition), 0, Mathf.FloorToInt((float)State.Duration)));
        // The host owns completion; clients can independently pause when the screen leaves their view.
        if (Authority && State.Playing && State.Duration > 0 && Position >= State.Duration) Finished();
        return true;
    }
    private bool VisibleTo(Camera camera)
    {
        if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), screen.bounds)) return false;
        // Require at least half of the projected screen to be inside the viewport before autoplay.
        var bounds = screen.bounds; var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity); var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var projected = camera.WorldToViewportPoint(corner); if (projected.z <= 0) return false;
            minimum = Vector2.Min(minimum, projected); maximum = Vector2.Max(maximum, projected);
        }
        float area = (maximum.x - minimum.x) * (maximum.y - minimum.y);
        float visibleArea = Mathf.Max(0, Mathf.Min(1, maximum.x) - Mathf.Max(0, minimum.x)) * Mathf.Max(0, Mathf.Min(1, maximum.y) - Mathf.Max(0, minimum.y));
        if (area <= 0 || visibleArea < area * .5f) return false;
        var center = screen.bounds.center; var direction = center - camera.transform.position;
        return !gameObject.scene.GetPhysicsScene().Raycast(camera.transform.position, direction.normalized, out var hit, direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.collider.transform.IsChildOf(transform);
    }
    private void LateUpdate()
    {
        if (youtube == null || Time.unscaledTime < nextBrowserFrame) return;
        nextBrowserFrame = Time.unscaledTime + .05f; youtube.Render(target);
    }
    public void PlayerPointer(Vector2 point, bool down, bool up) => youtube?.Mouse(point, down, up);
}
