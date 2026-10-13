using System;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

[Serializable] public struct LobbyScoreState { public string Player; public int Last, Best, Streak; }
public abstract class LobbyScore : LobbyInteractable
{
    [SerializeField] protected TMP_Text scoreLabel;
    protected readonly SyncVar<LobbyScoreState> score = new SyncVar<LobbyScoreState>();
    private LobbyScoreState offlineScore;
    public LobbyScoreState Score => NetworkLobby.Instance?.Offline == true ? offlineScore : score.Value;
    protected void Record(string player, int points)
    {
        if (!Authority || string.IsNullOrEmpty(player)) return;
        var previous = Score;
        var next = new LobbyScoreState { Player = player, Last = points, Best = Mathf.Max(previous.Best, points), Streak = previous.Player == player ? previous.Streak + 1 : 1 };
        if (NetworkLobby.Instance?.Offline == true) offlineScore = next; else score.Value = next;
    }
    public override void LobbyTick(float delta)
    {
        if (scoreLabel != null) scoreLabel.text = name.ToUpperInvariant() + "\n" + (Score.Player ?? "—") + "  " + Score.Last + "\nBEST " + Score.Best + " · STREAK " + Score.Streak;
    }
}
