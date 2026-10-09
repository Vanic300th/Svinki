using UnityEngine;

/// <summary>
/// Игрок глазами манекенов: где он стоит, откуда и куда смотрит, как быстро двигается, что на нём надето.
/// Вешается на объект игрока (рядом с GrayboxPlayerController) и сам записывается в PlayerRegistry.
/// В онлайне его добавляет NetworkPlayer, а взгляд берётся из него (IPlayerViewSource).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerOutfit))]
public sealed class PlayerAvatar : MonoBehaviour
{
    [Tooltip("Камера глаз этого игрока. Пусто = Main Camera (только для игрока, которым управляют с этого компьютера)")]
    [SerializeField] private Camera eyeCamera;

    private CharacterController characterController;
    private GrayboxPlayerController controller;
    private PlayerOutfit outfit;
    private IPlayerViewSource viewSource;

    /// <summary>Игроком управляют с этого компьютера. В одиночке — всегда.</summary>
    public bool IsLocal => viewSource == null || viewSource.IsLocalPlayer;

    /// <summary>Камера глаз — только у своего игрока. У чужих в онлайне камеры здесь нет.</summary>
    public Camera EyeCamera
    {
        get
        {
            if (eyeCamera == null && IsLocal) eyeCamera = Camera.main;
            return IsLocal ? eyeCamera : null;
        }
    }

    /// <summary>Точка у ног игрока.</summary>
    public Vector3 Position => transform.position;
    public bool IsAlive => GetComponent<PlayerKnockdown>()?.IsDead != true;
    /// <summary>Откуда игрок смотрит.</summary>
    public Vector3 EyePosition => TryGetView(out PlayerView view) ? view.Eye : transform.position + Vector3.up * 1.6f;
    /// <summary>Настоящая скорость игрока (м/с).</summary>
    public Vector3 Velocity => characterController != null ? characterController.velocity : Vector3.zero;
    /// <summary>0 — стоит в полный рост, 1 — полностью присел.</summary>
    public float CrouchAmount => controller != null ? controller.CrouchAmount : 0f;
    /// <summary>Собранная одежда этого игрока.</summary>
    public PlayerOutfit Outfit => outfit;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        controller = GetComponent<GrayboxPlayerController>();
        outfit = GetComponent<PlayerOutfit>();
        viewSource = GetComponent<IPlayerViewSource>();
    }

    private void OnEnable() => PlayerRegistry.Register(this);
    private void OnDisable() => PlayerRegistry.Unregister(this);

    /// <summary>
    /// Куда игрок смотрит прямо сейчас. Свой игрок — по камере (точно), чужой на сервере — по данным из сети.
    /// false — неизвестно (чужой игрок на обычном клиенте).
    /// </summary>
    public bool TryGetView(out PlayerView view)
    {
        Camera camera = EyeCamera;
        if (camera != null)
        {
            view = PlayerView.FromCamera(camera);
            return true;
        }
        if (viewSource != null && viewSource.HasView)
        {
            view = new PlayerView
            {
                Eye = viewSource.EyePosition,
                Rotation = viewSource.EyeRotation,
                FieldOfView = viewSource.FieldOfView,
                Aspect = viewSource.Aspect,
                IsRemote = true
            };
            return true;
        }
        view = default;
        return false;
    }
}
