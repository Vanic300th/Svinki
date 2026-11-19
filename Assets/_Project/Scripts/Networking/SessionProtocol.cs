using FishNet.Broadcast;
using System;
public enum SessionPhase : byte { Menu, Lobby, Loading, Round, Returning, Results, ChoosingGrandpaRoles, ChoosingGrandpaClasses }
public enum SessionAction : byte { Hello, Ready, Start, EndRound, ToggleAdmission, Kick, Leave, Appearance, Continue, FinishReady, RunwayReact, RunwayPose, RunwayControl, ReturnToLobby, GrandpaVolunteer, GrandpaConfirmClass }
public struct SessionRequest : IBroadcast
{
    public int Version;
    public SessionAction Action;
    public string Identity, Nickname, Target;
    public bool Value;
    public long Appearance;
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
    public SessionGameMode gameMode;
    public bool gameSelected;
    public double grandpaSelectionDeadline;
    public bool grandpaSoloChoice;
    public ParticipantSnapshot[] players = Array.Empty<ParticipantSnapshot>();
    public OutfitRoundResults results;
    public string runwayReaction;
    public int runwayReactionSequence;
    public float runwayElapsed;
}
[Serializable] public sealed class ParticipantSnapshot
{
    public string id, nickname;
    public bool ready, connected, spectator, finishReady, eliminated;
    public float reservation;
    public long appearance;
    public int colorSlot;
    public bool wantsGrandpa, grandpaClassReady;
    public GrandpaRole grandpaRole;
    public string pigClassId;
}
