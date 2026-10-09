using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>The host selects reachable, clear positions once per scene load. Existing network
/// transforms replicate NPC/cart poses; dynamically spawned toys/cameras carry their spawn pose.</summary>
public sealed class RoundWorldLayout : MonoBehaviour
{
    public struct Placement { public string Name; public Vector3 Position; public float Radius; }
    private readonly List<Placement> placements = new List<Placement>();
    private readonly Dictionary<Transform,float> pivotHeights = new Dictionary<Transform,float>();
    private readonly HashSet<Transform> relocating = new HashSet<Transform>();
    private readonly Collider[] overlaps = new Collider[128];
    private System.Random random;
    private StoreMap map;
    private NavMeshPath path;
    private Vector3 entrance;
    public int Seed { get; private set; }
    public IReadOnlyList<Placement> Placements => placements;
    public static RoundWorldLayout Begin(Scene scene)
    {
        if (NetworkLobby.Instance != null && !NetworkLobby.Instance.Offline && !NetworkLobby.Instance.IsHost) return null;
        var layout = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RoundWorldLayout>(true)).FirstOrDefault();
        if (layout != null) return layout;
        var root = new GameObject("Round world layout"); SceneManager.MoveGameObjectToScene(root, scene);
        layout = root.AddComponent<RoundWorldLayout>(); layout.Shuffle(Guid.NewGuid().GetHashCode()); return layout;
    }
    public void Shuffle(int seed)
    {
        Seed = seed; random = new System.Random(seed); placements.Clear(); relocating.Clear(); path = new NavMeshPath();
        map = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StoreMap>(true)).FirstOrDefault();
        var targets = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.GetComponent<ShoppingCart>() != null || t.GetComponent<ThrowableMannequin>() != null ||
                t.GetComponent<MannequinBrain>() != null || t.GetComponent<ThiefBrain>() != null).OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
        foreach (var target in targets)
        {
            relocating.Add(target);
            if(!pivotHeights.ContainsKey(target))pivotHeights[target]=NavMesh.SamplePosition(target.position,out var ground,3,NavMesh.AllAreas)?Mathf.Clamp(target.position.y-ground.position.y,0,.5f):Mathf.Clamp(target.position.y,0,.5f);
        }
        if (!NavMesh.SamplePosition(new Vector3(0, 0, -3), out var start, 4, NavMesh.AllAreas))
            throw new InvalidOperationException("Round spawn requires a connected store NavMesh.");
        entrance = start.position;
        // Large carts are placed first, so they get enough clearance for their basket and handle.
        foreach (var target in targets.OrderByDescending(t => t.GetComponent<ShoppingCart>() != null))
        {
            bool cart = target.GetComponent<ShoppingCart>() != null;
            if (!TryTakePoint(target.name, cart ? 1.5f : .65f, out var point))
                throw new InvalidOperationException("Could not find a safe round spawn for " + target.name);
            var rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
            point.y += pivotHeights[target]; // Preserve authored pivot height, not an arbitrary model centre.
            var agent = target.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(point);
            target.SetPositionAndRotation(point, rotation);
            if (target.TryGetComponent<Rigidbody>(out var body)) { body.position = point; body.rotation = rotation; if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; } }
            target.GetComponent<ShoppingCart>()?.SetSpawnHome(point, rotation);
            target.GetComponent<ThrowableMannequin>()?.SetSpawnHome(point, rotation);
        }
        Physics.SyncTransforms();
    }
    public bool TryTakePoint(string name, float radius, out Vector3 point)
    {
        point = default; if (map == null || random == null) return false;
        var floors = map.Floors; float total = floors.Sum(f => f.size.x * f.size.y);
        for (int attempt = 0; attempt < 1600; attempt++)
        {
            float pick = (float)random.NextDouble() * total; var floor = floors[0];
            foreach (var f in floors) { floor = f; pick -= f.size.x * f.size.y; if (pick <= 0) break; }
            var offset = Quaternion.Euler(0, -floor.angle, 0) * new Vector3(((float)random.NextDouble() - .5f) * floor.size.x, 0, ((float)random.NextDouble() - .5f) * floor.size.y);
            var candidate = new Vector3(floor.center.x, 0, floor.center.y) + offset;
            if (candidate.z < 9 || !NavMesh.SamplePosition(candidate, out var hit, .6f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y) > .3f) continue;
            candidate = hit.position;
            if (placements.Any(p => Vector3.Distance(p.Position, candidate) < p.Radius + radius + 2f)) continue;
            if (!NavMesh.CalculatePath(entrance, candidate, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            int count = gameObject.scene.GetPhysicsScene().OverlapBox(candidate + Vector3.up * 1.2f,
                new Vector3(radius, 1.1f, radius), overlaps, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) continue;
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                var collider = overlaps[i];
                if (collider == null || relocating.Any(t => collider.transform.IsChildOf(t)) || collider.GetComponentInParent<PlayerAvatar>() != null) continue;
                blocked = true; break;
            }
            if (blocked) continue;
            point = candidate; placements.Add(new Placement { Name = name, Position = point, Radius = radius }); return true;
        }
        return false;
    }
}
