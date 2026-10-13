using System.Linq;
using UnityEngine;

public sealed partial class NetworkLobby
{
    private float runwayStartedAt, runwayReceivedAt, nextRunwayPublish;
    public float RunwayElapsed => Offline ? Mathf.Max(0, Time.unscaledTime - runwayStartedAt) :
        Snapshot.phase == SessionPhase.Results ? Snapshot.runwayElapsed + Mathf.Max(0, Time.unscaledTime - runwayReceivedAt) : 0;
    private float ServerRunwayElapsed => Mathf.Max(0, Time.unscaledTime - runwayStartedAt);
    private void BeginRunwayClock() { runwayStartedAt = Time.unscaledTime; nextRunwayPublish = 0; }
    public void SubmitRunwayMovement(RunwayMovement movement)
    {
        if (Results == null) return;
        if (Offline)
        {
            int index = Mathf.FloorToInt(RunwayElapsed / FinalRunwayView.SecondsPerPlayer);
            if (index < offlineResults.entries.Length) offlineResults.entries[index].movement = movement;
        }
        else Send(SessionAction.RunwayControl, false, JsonUtility.ToJson(movement));
    }
    private void AcceptRunwayMovement(Member member, string payload)
    {
        if (phase != SessionPhase.Results || roundResults == null || payload == null || payload.Length > 350 || Time.unscaledTime < member.NextRunwayControl) return;
        int index = Mathf.FloorToInt(ServerRunwayElapsed / FinalRunwayView.SecondsPerPlayer);
        if (index >= roundResults.entries.Length || roundResults.entries[index].id != member.Id) return;
        RunwayMovement value;
        try { value = JsonUtility.FromJson<RunwayMovement>(payload); } catch { return; }
        if (!float.IsFinite(value.position.x) || !float.IsFinite(value.position.y) || !float.IsFinite(value.position.z) ||
            !float.IsFinite(value.yaw) || !float.IsFinite(value.crouch) || !float.IsFinite(value.emoteAt) || value.emote < 0 || value.emote > 7) return;
        member.NextRunwayControl = Time.unscaledTime + .04f;
        value.position = new Vector3(Mathf.Clamp(value.position.x, -.85f, .85f), Mathf.Clamp(value.position.y, 0, 1.3f), Mathf.Clamp(value.position.z, -3.5f, 1.1f));
        value.crouch = Mathf.Clamp01(value.crouch); value.emoteAt = Mathf.Clamp(value.emoteAt, 0, ServerRunwayElapsed);
        roundResults.entries[index].movement = value;
    }
    private void TickRunwayClock()
    {
        if (!hosting || phase != SessionPhase.Results || Time.unscaledTime < nextRunwayPublish) return;
        nextRunwayPublish = Time.unscaledTime + .2f; Publish();
    }
}
