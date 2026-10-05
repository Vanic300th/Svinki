using FishNet.Broadcast;
using System;
public enum SessionPhase : byte { Menu, Lobby, Loading, Round, Returning }
public enum SessionAction : byte { Hello, Ready, Start, EndRound, ToggleAdmission, Kick, Leave }
public struct SessionRequest : IBroadcast
{
    public int Version;
    public SessionAction Action;
    public string Identity, Nickname, Target;
    public bool Value;
}
public struct SessionMessage : IBroadcast
{
    public string Snapshot;
    public string Error;
    public bool Terminal;
}
[Serializable] public sealed class SessionSnapshot
{
    public SessionPhase phase;
    public string code, host;
    public int round;
    public bool closed;
    public ParticipantSnapshot[] players = Array.Empty<ParticipantSnapshot>();
}
[Serializable] public sealed class ParticipantSnapshot
{
    public string id, nickname;
    public bool ready, connected, spectator;
    public float reservation;
}
