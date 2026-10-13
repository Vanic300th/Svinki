using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Сталкер в онлайне: мозг, агент и видимость работают только на сервере, клиенты только показывают
/// (позиция — NetworkTransform, анимация — NetworkMannequinAnimation).
/// Видимость сервер считает сам (MannequinVisibility проверяет всех игроков своей комнаты по их взгляду),
/// здесь только выбирается цель — ближайший игрок комнаты.
/// </summary>
[DefaultExecutionOrder(-900)]
[RequireComponent(typeof(MannequinBrain), typeof(MannequinVisibility))]
public sealed class NetworkMannequinAuthority : NetworkBehaviour
{
    private MannequinBrain brain;
    private MannequinVisibility visibility;
    private NavMeshAgent agent;
    private Transform currentTarget;
    private readonly SyncVar<bool> roundActive = new SyncVar<bool>(true);
    public bool RoundActive => roundActive.Value;
    public void SetRoundActive(bool value) { roundActive.Value = value; ApplyRoundActive(value); }
    private void ApplyRoundActive(bool value)
    {
        if (value) return;
        brain.enabled = false; visibility.enabled = false;
        if (agent != null) agent.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
    }

    private void Awake()
    {
        brain = GetComponent<MannequinBrain>();
        visibility = GetComponent<MannequinVisibility>();
        agent = GetComponent<NavMeshAgent>();
        roundActive.OnChange += (previous, next, asServer) => ApplyRoundActive(next);
    }

    private void Start()
    {
        // Network prefabs start with navigation disabled: guests only display server poses.
        // Start runs after the scene's NavMeshSurface has registered/baked its data.
        if (agent != null)
            agent.enabled = roundActive.Value && (NetworkLobby.Instance == null || NetworkLobby.Instance.Offline || FishNet.InstanceFinder.IsServerStarted);
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        ApplyRoundActive(roundActive.Value);
        if (!IsServerStarted)
        {
            brain.enabled = false;
            visibility.enabled = false;
            if (agent != null) agent.enabled = false;
        }
    }

    private void Update()
    {
        if (NetworkObject == null || !IsServerInitialized || !roundActive.Value) return;
        PlayerAvatar closest = PlayerRegistry.Nearest(transform.position, gameObject.scene);
        Transform next = closest != null ? closest.transform : null;
        if (next == currentTarget) return;
        currentTarget = next;
        brain.SetTarget(next);
    }
}
