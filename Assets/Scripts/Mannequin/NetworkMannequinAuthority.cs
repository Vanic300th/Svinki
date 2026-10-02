using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(MannequinBrain), typeof(MannequinVisibility))]
public sealed class NetworkMannequinAuthority : NetworkBehaviour
{
    private MannequinBrain brain;
    private MannequinVisibility visibility;
    private NavMeshAgent agent;

    private void Awake()
    {
        brain = GetComponent<MannequinBrain>();
        visibility = GetComponent<MannequinVisibility>();
        agent = GetComponent<NavMeshAgent>();
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (!IsServerStarted)
        {
            brain.enabled = false;
            if (agent != null) agent.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsServerInitialized) return;
        NetworkPlayer closest = null;
        float best = float.PositiveInfinity;
        bool seen = false;
        foreach (NetworkPlayer player in FindObjectsByType<NetworkPlayer>())
        {
            if (player.gameObject.scene != gameObject.scene) continue;
            float distance = (player.transform.position - transform.position).sqrMagnitude;
            if (distance < best) { best = distance; closest = player; }
            if (IsSeenBy(player)) seen = true;
        }
        brain.SetTarget(closest != null ? closest.transform : null);
        visibility.SetServerSeen(seen);
    }

    private bool IsSeenBy(NetworkPlayer player)
    {
        Vector3 eye = player.transform.position + Vector3.up * 1.6f;
        Vector3 point = transform.position + Vector3.up * 1.4f;
        Vector3 direction = point - eye;
        float distance = direction.magnitude;
        if (distance > 60f || distance < 0.01f) return false;
        Vector3 look = Quaternion.Euler(player.ViewPitch, player.ViewYaw, 0f) * Vector3.forward;
        if (Vector3.Angle(look, direction) > 47f) return false;
        var physicsScene = gameObject.scene.GetPhysicsScene();
        if (physicsScene.Raycast(eye, direction.normalized, out RaycastHit hit, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.collider.transform.IsChildOf(transform);
        return true;
    }
}
