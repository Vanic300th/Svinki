using System;
using System.Linq;
using FishNet.Object.Synchronizing;
using UnityEngine;

[Serializable] public struct AirHockeyState { public string A, B; public Vector2 PaddleA, PaddleB; public int ScoreA, ScoreB; }
public sealed class AirHockeyTable : LobbyInteractable
{
    [SerializeField] private Rigidbody paddleA, paddleB;
    [SerializeField] private LobbyProp puck;
    [SerializeField] private Vector2 halfSize = new Vector2(1.3f, 2.2f);
    [SerializeField, Min(1)] private float paddleSpeed = 8;
    [SerializeField, Min(4)] private float controlRange = 6;
    private readonly SyncVar<AirHockeyState> state = new SyncVar<AirHockeyState>();
    private AirHockeyState offlineState;
    private float lastA, lastB;
    private AirHockeyState State => NetworkLobby.Instance?.Offline == true ? offlineState : state.Value;
    public string Status => "AIR HOCKEY  " + State.ScoreA + " : " + State.ScoreB + "\n" + PlayerName(State.A) + " / " + PlayerName(State.B);
    private static string PlayerName(string id) => string.IsNullOrEmpty(id) ? "Free" : id == "offline" ? NetworkLobby.Instance.Nickname :
        NetworkLobby.Instance.Snapshot.players.FirstOrDefault(p => p.id == id)?.nickname ?? "Player";
    private void Set(AirHockeyState next) { if (NetworkLobby.Instance?.Offline == true) offlineState = next; else state.Value = next; }
    public override void OnStartServer() { base.OnStartServer(); Set(new AirHockeyState { PaddleA = new Vector2(0, -1.3f), PaddleB = new Vector2(0, 1.3f) }); }
    private void Start() { if (NetworkLobby.Instance?.Offline == true) offlineState = new AirHockeyState { PaddleA = new Vector2(0, -1.3f), PaddleB = new Vector2(0, 1.3f) }; }
    public override string Prompt(PlayerAvatar player) => "E — air hockey · control a paddle";
    public override void Interact(PlayerAvatar player) => LobbyPanel.Open(this, player);
    public override void Apply(PlayerAvatar player, LobbyAction action, string text, int index, Vector2[] points)
    {
        string id = Identity(player); var next = State;
        if (action == LobbyAction.Toggle)
        {
            if (next.A == id || next.B == id) return;
            if (string.IsNullOrEmpty(next.A)) next.A = id; else if (string.IsNullOrEmpty(next.B)) next.B = id;
        }
        else if (action == LobbyAction.Reset) { if (next.A == id) next.A = null; if (next.B == id) next.B = null; }
        else if (action == LobbyAction.Stroke && points != null && points.Length > 0)
        {
            var point = points[points.Length - 1];
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return;
            float now = Time.unscaledTime;
            if (next.A == id)
            {
                var wanted = new Vector2((point.x * 2 - 1) * halfSize.x, -.25f - point.y * (halfSize.y - .5f));
                next.PaddleA = Vector2.MoveTowards(next.PaddleA, wanted, paddleSpeed * Mathf.Min(.15f, now - lastA)); lastA = now;
            }
            else if (next.B == id)
            {
                var wanted = new Vector2((point.x * 2 - 1) * halfSize.x, .25f + point.y * (halfSize.y - .5f));
                next.PaddleB = Vector2.MoveTowards(next.PaddleB, wanted, paddleSpeed * Mathf.Min(.15f, now - lastB)); lastB = now;
            }
        }
        Set(next);
    }
    public override void LobbyTick(float delta)
    {
        var next = State;
        if (Authority)
        {
            // Closing the panel after walking away cannot send a proximity-validated Reset.
            // Release stale seats here so a connected player cannot reserve a paddle forever.
            if (!CanKeepPaddle(next.A)) next.A = null;
            if (!CanKeepPaddle(next.B)) next.B = null;
            if (next.A != State.A || next.B != State.B) Set(next);
        }
    }
    private bool CanKeepPaddle(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return PlayerRegistry.Players.Any(player => player != null && player.isActiveAndEnabled &&
            player.gameObject.scene == gameObject.scene && Identity(player) == id && player.IsAlive &&
            player.GetComponent<PlayerKnockdown>()?.IsDown != true &&
            (player.Position - transform.position).sqrMagnitude <= controlRange * controlRange &&
            (NetworkLobby.Instance?.Offline == true || NetworkLobby.Instance?.CanUseLobby(player.GetComponent<NetworkPlayer>()) == true));
    }
    public void PhysicsFrame() { Move(paddleA, State.PaddleA); Move(paddleB, State.PaddleB); }
    private void Move(Rigidbody paddle, Vector2 point)
    {
        var position = transform.TransformPoint(new Vector3(point.x, 1.15f, point.y));
        if (Authority) paddle.MovePosition(Vector3.MoveTowards(paddle.position, position, paddleSpeed * Time.fixedDeltaTime));
        else paddle.position = Vector3.Lerp(paddle.position, position, Time.fixedDeltaTime * 15);
    }
    public void Goal(LobbyProp hit, bool sideA)
    {
        if (!Authority || hit != puck || hit.Holder != null) return;
        var next = State; if (sideA) next.ScoreB++; else next.ScoreA++; Set(next); puck.ResetHome();
    }
}
