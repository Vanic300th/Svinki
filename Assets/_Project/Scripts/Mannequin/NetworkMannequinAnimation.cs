using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Анимация манекена в онлайне. Мозг работает только на сервере и отдаёт команды MannequinAnimator
/// (пошёл, дотягивает позу, замер, ударил). Этот компонент пересылает те же команды клиентам,
/// и их MannequinAnimator проигрывает их сам — поэтому «пойманный на движении» манекен
/// у всех дотягивает позу одинаково. Скорость шага — отдельным значением.
/// Общий для всех ходячих манекенов (Сталкер, Воришка).
/// </summary>
[RequireComponent(typeof(MannequinAnimator))]
public sealed class NetworkMannequinAnimation : NetworkBehaviour
{
    // Что манекен делает сейчас — для игроков, которые зашли позже
    private readonly SyncVar<byte> lastCommand = new SyncVar<byte>();
    private readonly SyncVar<float> moveSpeed = new SyncVar<float>(new SyncTypeSettings(0.1f));

    private MannequinAnimator anim;

    private bool AsServer => NetworkObject != null && IsServerInitialized;
    private bool AsClientOnly => NetworkObject != null && IsClientInitialized && !IsServerInitialized;

    private void Awake()
    {
        anim = GetComponent<MannequinAnimator>();
        anim.Commanded += OnCommanded;
    }

    private void OnDestroy()
    {
        if (anim != null) anim.Commanded -= OnCommanded;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (IsServerInitialized) return; // хост: аниматор и так управляется мозгом
        // Зашёл позже — ставим манекена в то, что он делает сейчас
        switch ((MannequinAnimator.Command)lastCommand.Value)
        {
            case MannequinAnimator.Command.StartMoving: anim.StartMoving(); break;
            case MannequinAnimator.Command.Attack: anim.PlayAttack(); break;
            default: anim.Freeze(); break;
        }
    }

    private void OnCommanded(MannequinAnimator.Command command, float duration)
    {
        if (!AsServer) return;
        lastCommand.Value = (byte)command;
        CommandObserversRpc((byte)command, duration);
    }

    [ObserversRpc(ExcludeServer = true)]
    private void CommandObserversRpc(byte command, float duration)
    {
        anim.Execute((MannequinAnimator.Command)command, duration);
    }

    private void Update()
    {
        if (AsServer)
        {
            if (Mathf.Abs(moveSpeed.Value - anim.MoveSpeed) > 0.05f) moveSpeed.Value = anim.MoveSpeed;
        }
        else if (AsClientOnly)
        {
            anim.SetMoveSpeed(moveSpeed.Value);
        }
    }
}
