using System.Linq;
using UnityEngine;

/// <summary>One scheduler for props, labels, assignment and cleanup; the host owns gameplay.</summary>
public sealed class LobbyActivityManager : MonoBehaviour
{
    [SerializeField, Range(.05f, .5f)] private float tickInterval = .1f;
    private LobbyInteractable[] items;
    private float nextTick;
    private LobbyProp[] props;
    private AirHockeyTable[] tables;
    private void Start() { items = GetComponentsInChildren<LobbyInteractable>(true); props = items.OfType<LobbyProp>().ToArray(); tables = items.OfType<AirHockeyTable>().ToArray(); }
    private void FixedUpdate() { if (tables != null) foreach (var table in tables) if (table != null && table.isActiveAndEnabled) table.PhysicsFrame(); }
    private void Update()
    {
        if (items == null) return;
        foreach (var prop in props) if (prop != null && prop.isActiveAndEnabled) prop.VisualFrame();
        if (Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + tickInterval;
        foreach (var item in items) if (item != null && item.isActiveAndEnabled) item.LobbyTick(tickInterval);
        var lobby = NetworkLobby.Instance;
        if (lobby == null || !lobby.Offline && !lobby.IsHost) return;
        foreach (var locker in items.OfType<PlayerLocker>())
        {
            var member = lobby.Snapshot.players.FirstOrDefault(p => p.colorSlot == locker.Slot);
            locker.Assign(lobby.Offline && locker.Slot == 0 ? "offline" : member?.id ?? "",
                lobby.Offline && locker.Slot == 0 ? lobby.Nickname : member?.nickname ?? "UNASSIGNED");
        }
    }
}
