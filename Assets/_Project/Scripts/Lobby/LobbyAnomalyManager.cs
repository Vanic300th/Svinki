using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>One quiet anomaly choice between rounds. No roaming brain or combat component.</summary>
public sealed class LobbyAnomalyManager : NetworkBehaviour
{
    [SerializeField] private LobbyAnomalyEvent[] events;
    [SerializeField, Range(0, 1)] private float chancePerRound = 1f;
    private readonly SyncVar<int> choice = new SyncVar<int>();
    private void Awake() => choice.OnChange += (previous, next, asServer) => Apply(next);
    public override void OnStartServer() { base.OnStartServer(); Choose(); }
    public override void OnStartClient() { base.OnStartClient(); Apply(choice.Value); }
    private void Start() { if (NetworkLobby.Instance?.Offline == true) Choose(); }
    private void Choose()
    {
        var lobby = NetworkLobby.Instance;
        if (lobby == null || events.Length == 0) return;
        int round = lobby.LobbyRound;
        if (lobby.LastLobbyAnomalyRound != round)
        {
            int variants = 1;
            foreach (var anomaly in events) if (anomaly != null) variants = Mathf.Max(variants, anomaly.VariantCount);
            if (lobby.LastLobbyAnomalyRound <= 0) lobby.LobbyAnomalyChoice = Random.Range(0, variants);
            else if (variants > 1 && Random.value < chancePerRound)
                lobby.LobbyAnomalyChoice = (lobby.LobbyAnomalyChoice + Random.Range(1, variants)) % variants;
            lobby.LastLobbyAnomalyRound = round;
        }
        if (!lobby.Offline) choice.Value = lobby.LobbyAnomalyChoice;
        Apply(lobby.LobbyAnomalyChoice);
    }
    private void Apply(int value) { foreach (var anomaly in events) if (anomaly != null) anomaly.Apply(value); }
}
