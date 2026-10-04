using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Сталкер в онлайне: мозг, агент и видимость работают только на сервере, клиенты только показывают
/// (позиция — NetworkTransform, анимация — NetworkMannequinAnimation).
/// Видимость сервер считает сам (MannequinVisibility проверяет всех игроков своей комнаты по их взгляду),
/// здесь только выбирается цель — ближайший игрок комнаты.
/// </summary>
[RequireComponent(typeof(MannequinBrain), typeof(MannequinVisibility))]
public sealed class NetworkMannequinAuthority : NetworkBehaviour
{
    private MannequinBrain brain;
    private MannequinVisibility visibility;
    private NavMeshAgent agent;
    private Transform currentTarget;

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

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Копии комнат на сервере лежат друг на друге, а обход агентов в Unity общий на все сцены —
        // без этого манекены разных комнат расталкивали бы друг друга
        if (agent != null) agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    private void Update()
    {
        if (NetworkObject == null || !IsServerInitialized) return;
        PlayerAvatar closest = PlayerRegistry.Nearest(transform.position, gameObject.scene);
        Transform next = closest != null ? closest.transform : null;
        if (next == currentTarget) return;
        currentTarget = next;
        brain.SetTarget(next);
    }
}
