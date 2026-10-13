#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class LobbyProbeState
{
    public LobbyLiftState lift;
    public LobbyScoreState basketball, darts;
    public LobbyMediaState tv, music;
    public int boardCommands, queue;
    public string tvError, musicError, held, hockey;
    public double tvActual, musicActual;
    public bool navInLobby, navAtSpawn;
    public Vector3 dartboardPosition, dartboardForward;
    public LockerProbe[] lockers;
    public PropProbe[] props;
}
[Serializable] public sealed class LockerProbe { public string owner; public int slot, photos; public string[] hashes; public Quaternion door; }
[Serializable] public sealed class PropProbe { public string name, holder; public Vector3 position, velocity; public bool sleeping, canScore; }
public static class LobbyDevelopmentProbe
{
    private static T[] Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
    private static PlayerAvatar Local => PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal);
    public static LobbyProbeState Read()
    {
        var lobby = NetworkLobby.Instance;
        var musicSource = Find<LobbyJukebox>().FirstOrDefault()?.GetComponent<AudioSource>();
        return new LobbyProbeState
        {
            lift = Find<LobbyFreightElevator>().FirstOrDefault()?.State ?? default,
            dartboardPosition = Find<Dartboard>().FirstOrDefault()?.transform.position ?? default,
            dartboardForward = Find<Dartboard>().FirstOrDefault()?.transform.forward ?? default,
            basketball = Find<BasketballHoop>().FirstOrDefault()?.Score ?? default, darts = Find<Dartboard>().FirstOrDefault()?.Score ?? default,
            boardCommands = Find<DrawingBoard>().FirstOrDefault()?.CommandCount ?? 0,
            tv = Find<LobbyTelevision>().FirstOrDefault()?.State ?? default, music = Find<LobbyJukebox>().FirstOrDefault()?.State ?? default,
            tvError = Find<LobbyTelevision>().FirstOrDefault()?.LocalError, musicError = Find<LobbyJukebox>().FirstOrDefault()?.LocalError,
            queue = Find<LobbyJukebox>().FirstOrDefault()?.Queue.Count() ?? 0,
            hockey = Find<AirHockeyTable>().FirstOrDefault()?.Status,
            tvActual = Find<LobbyTelevision>().FirstOrDefault()?.GetComponent<UnityEngine.Video.VideoPlayer>().time ?? 0,
            musicActual = musicSource != null && musicSource.clip != null ? musicSource.time : 0,
            held = Local?.GetComponent<PlayerLobbyHands>()?.Held?.name,
            navInLobby = UnityEngine.AI.NavMesh.SamplePosition(new Vector3(0, .1f, -22), out _, .5f, UnityEngine.AI.NavMesh.AllAreas),
            navAtSpawn = UnityEngine.AI.NavMesh.SamplePosition(new Vector3(0, .1f, -3), out _, .5f, UnityEngine.AI.NavMesh.AllAreas),
            lockers = Find<PlayerLocker>().Select(l => new LockerProbe { owner = l.OwnerId, slot = l.Slot, door = l.transform.Find("Hinged door").localRotation,
                photos = Enumerable.Range(0, 6).Count(i => lobby.LockerPhoto(l.OwnerId, i) != null),
                hashes = Enumerable.Range(0, 6).Select(i => {var image=lobby.LockerPhoto(l.OwnerId,i); if(image==null)return "";using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(image.EncodeToPNG()));}).ToArray() }).OrderBy(l=>l.slot).ToArray(),
            props = Find<LobbyProp>().Select(p => new PropProbe { name=p.name,holder=p.Holder?.GetComponent<NetworkPlayer>()?.ParticipantName,position=p.transform.position,velocity=p.GetComponent<Rigidbody>().linearVelocity,canScore=p.CanScore,sleeping=p.GetComponent<Rigidbody>().IsSleeping() }).OrderBy(p=>p.name).ToArray()
        };
    }
    private static void Move(Vector3 position)
    {
        if (Local == null) return;
        var controller = Local.GetComponent<CharacterController>(); controller.enabled = false; Local.transform.position = position; controller.enabled = true;
    }
    public static void Command(string action, string target, int index)
    {
        if (action == "social-lift-in") { var lift = Find<LobbyFreightElevator>().FirstOrDefault(); if(lift!=null)Move(lift.transform.TransformPoint(new Vector3(index * .8f, .1f, 0))); return; }
        if (action == "social-lift-out") { var lift = Find<LobbyFreightElevator>().FirstOrDefault(); if(lift!=null)Move(lift.transform.TransformPoint(new Vector3(0, .1f, -3))); return; }
        if (action == "social-place")
        {
            Vector3[] positions = {new Vector3(0,.1f,-22),new Vector3(7,.1f,-16.6f),new Vector3(0,.1f,-29),new Vector3(-9,.1f,-26.3f),new Vector3(-4.8f,.1f,-28.5f),new Vector3(8,.1f,-20)};
            Move(positions[Mathf.Clamp(index,0,positions.Length-1)]); return;
        }
        if(Local==null)return;
        if (action.StartsWith("social-locker"))
        {
            var locker=Find<PlayerLocker>().First(l=>l.Slot==index);Move(locker.transform.position+new Vector3(0,.1f,-1.6f));
            if(action=="social-locker-approach")return;
            locker.Request(Local,action=="social-locker-photo"?LobbyAction.AssignPhoto:LobbyAction.Toggle, target??"",0); return;
        }
        if (action.StartsWith("social-board"))
        {
            var board=Find<DrawingBoard>().First();Move(board.transform.position+new Vector3(0,-1.7f,1.8f));
            if(action=="social-board-clear")board.Request(Local,LobbyAction.ClearBoard);
            else board.Request(Local,LobbyAction.Stroke,"draw",3,new[]{new Vector2(.2f,.2f),new Vector2(action=="social-board-invalid"?-1:.6f,.7f)});
            return;
        }
        if (action=="social-prop" || action=="social-prop-approach")
        {
            var prop=Find<LobbyProp>().First(p=>p.name==target);Move(prop.transform.position+new Vector3(0,0,-1.5f));
            if(Local.transform.position.y<.1f)Move(new Vector3(Local.Position.x,.1f,Local.Position.z));
            if(action=="social-prop")prop.Request(Local,LobbyAction.Toggle);return;
        }
        if(action=="social-release"){Local.GetComponent<PlayerLobbyHands>()?.Release(index==1);return;}
        if(action.StartsWith("social-media"))
        {
            LobbyMediaPlayer media=index==0?(LobbyMediaPlayer)Find<LobbyTelevision>().First():Find<LobbyJukebox>().First();
            Move(media.transform.position+media.transform.rotation*new Vector3(0,0,-1.7f));Move(new Vector3(Local.Position.x,.1f,Local.Position.z));
            media.Request(Local,action=="social-media-pause"?LobbyAction.PlayPause:action=="social-media-skip"?LobbyAction.Skip:action=="social-media-stop"?LobbyAction.Stop:LobbyAction.Load,target??"");
            return;
        }
        if (action == "social-hockey")
        {
            var table=Find<AirHockeyTable>().First();Move(table.transform.position+new Vector3(0,.1f,-3));
            table.Request(Local,LobbyAction.Toggle);return;
        }
        if (action == "social-hockey-move") { Find<AirHockeyTable>().First().Request(Local,LobbyAction.Stroke,"",0,new[]{new Vector2(.7f,.4f)});return; }
        if (action == "social-hockey-goal" && NetworkLobby.Instance.IsHost)
        {
            var table=Find<AirHockeyTable>().First();var puck=Find<LobbyProp>().First(p=>p.Kind==LobbyPropKind.Puck);
            var start=table.transform.position+new Vector3(0,1.15f,-2.1f);puck.transform.position=start;puck.GetComponent<Rigidbody>().position=start;puck.GetComponent<Rigidbody>().linearVelocity=Vector3.back*3;puck.GetComponent<Rigidbody>().WakeUp();return;
        }
        if ((action=="social-basket-shot" || action=="social-dart-shot") && NetworkLobby.Instance.IsHost)
        {
            var prop=Find<LobbyProp>().First(p=>p.Kind==(action=="social-basket-shot"?LobbyPropKind.Basketball:LobbyPropKind.Dart));
            prop.ResetHome();Move(prop.transform.position+new Vector3(0,.1f,-1));prop.Apply(Local,LobbyAction.Toggle,"",0,null);prop.Release(true);
            var body=prop.GetComponent<Rigidbody>();
            if(action=="social-basket-shot")
            {
                var hoop=Find<BasketballHoop>().First();var point=hoop.transform.TransformPoint(new Vector3(0,3.6f,-.35f));
                Move(hoop.transform.position + new Vector3(-2,.1f,-2));prop.transform.position=point;body.position=point;body.linearVelocity=Vector3.down*4;body.WakeUp();
            }
            else
            {
                var board=Find<Dartboard>().First();Move(board.transform.position - board.transform.forward * 2 + Vector3.down * 1.6f);
                var point=board.transform.position-board.transform.forward*.6f;var rotation=Quaternion.LookRotation(board.transform.forward);
                prop.transform.SetPositionAndRotation(point,rotation);body.position=point;body.rotation=rotation;body.linearVelocity=board.transform.forward*12;body.WakeUp();
            }
            // Test-only teleports must be visible to the next physics step in this frame.
            Physics.SyncTransforms();
            return;
        }
        if(action=="social-prop-lost"&&NetworkLobby.Instance.IsHost){var prop=Find<LobbyProp>().First(p=>p.name==target);var body=prop.GetComponent<Rigidbody>();body.position=Vector3.down*20;body.linearVelocity=Vector3.zero;body.WakeUp();return;}
    }
}
#endif
