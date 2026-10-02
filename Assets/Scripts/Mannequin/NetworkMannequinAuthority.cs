using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Сталкер в онлайне: мозг, агент и видимость работают только на сервере, клиенты только показывают.
/// Видимость сервер считает сам (MannequinVisibility проверяет всех игроков своей комнаты по их взгляду),
/// здесь только выбирается цель — ближайший игрок комнаты.
/// </summary>
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
            visibility.enabled = false;
            if (agent != null) agent.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsServerInitialized) return;
        NetworkPlayer closest = null;
        float best = float.PositiveInfinity;
        foreach (NetworkPlayer player in FindObjectsByType<NetworkPlayer>())
        {
            if (player.gameObject.scene != gameObject.scene) continue;
            float distance = (player.transform.position - transform.position).sqrMagnitude;
            if (distance < best) { best = distance; closest = player; }
        }
        brain.SetTarget(closest != null ? closest.transform : null);
    }
}
