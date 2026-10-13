using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

[Serializable]
public struct LobbyLiftState
{
    public int Inside, Required;
    public bool Closing;
    public double StartedAt;
}

/// <summary>The existing session manager decides who participates and when a run may start or finish.
/// Only occupancy changes and the start/cancel of a countdown cross the network.</summary>
public sealed class LobbyFreightElevator : NetworkBehaviour
{
    [SerializeField] private Vector3 cabinCenter = new Vector3(0, 1.35f, 0);
    [SerializeField] private Vector3 cabinSize = new Vector3(5.4f, 2.7f, 3.4f);
    [SerializeField, Min(1)] private float closingSeconds = 3;
    [SerializeField] private Transform leftDoor, rightDoor;
    [SerializeField] private Light cabinLight;
    [SerializeField] private TMP_Text sign;
    [SerializeField] private string destinationTitle = "FACTORY RUN";
    private readonly SyncVar<LobbyLiftState> state = new SyncVar<LobbyLiftState>();
    private LobbyLiftState offlineState;
    private Vector3 leftOpen, rightOpen;
    public LobbyLiftState State => NetworkLobby.Instance?.Offline == true ? offlineState : state.Value;
    public bool CountdownFinished => State.Closing && Clock - State.StartedAt >= closingSeconds;
    public double Clock => NetworkObject != null && IsSpawned ? TimeManager.TicksToTime(TimeManager.Tick) : Time.unscaledTimeAsDouble;
    public bool Contains(Vector3 feet)
    {
        Vector3 point = transform.InverseTransformPoint(feet) - cabinCenter;
        Vector3 half = cabinSize * .5f;
        return Mathf.Abs(point.x) < half.x && Mathf.Abs(point.z) < half.z && point.y >= -half.y - .15f && point.y < half.y;
    }
    private void Awake()
    {
        if (leftDoor != null) leftOpen = leftDoor.localPosition;
        if (rightDoor != null) rightOpen = rightDoor.localPosition;
    }
    public bool Step(int inside, int required, bool eligible)
    {
        bool offline = NetworkLobby.Instance?.Offline == true;
        if (!offline && !IsServerInitialized) return false;
        var next = State;
        bool all = eligible && required > 0 && inside == required;
        if (all && !next.Closing) { next.Closing = true; next.StartedAt = Clock; }
        if (!all) { next.Closing = false; next.StartedAt = 0; }
        next.Inside = inside; next.Required = required;
        if (offline) offlineState = next;
        else if (next.Inside != State.Inside || next.Required != State.Required || next.Closing != State.Closing || next.StartedAt != State.StartedAt) state.Value = next;
        return all && Clock - next.StartedAt >= closingSeconds;
    }
    private void Update()
    {
        var current = State;
        float amount = current.Closing ? Mathf.Clamp01((float)(Clock - current.StartedAt) / closingSeconds) : 0;
        // Retract the doors on cancellation so leaving never traps the remaining players.
        float target = amount * 2.75f;
        if (leftDoor != null) leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, leftOpen + Vector3.right * target, Time.unscaledDeltaTime * 8);
        if (rightDoor != null) rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, rightOpen + Vector3.left * target, Time.unscaledDeltaTime * 8);
        if (cabinLight != null) cabinLight.intensity = current.Closing && amount > .65f && Mathf.Sin(amount * 80) > .35f ? .3f : 2;
        if (sign != null) sign.text = current.Closing ? destinationTitle + "\nDEPARTING…" : destinationTitle + "\nENTER TOGETHER  " + current.Inside + " / " + current.Required;
    }
}
