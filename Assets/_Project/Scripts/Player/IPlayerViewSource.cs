using UnityEngine;

/// <summary>
/// Откуда известно, куда смотрит игрок, если у этого компьютера нет его камеры.
/// В онлайне это NetworkPlayer: на сервере камер нет, но сервер знает позицию игрока,
/// поворот взгляда (приходит с вводом) и угол обзора с пропорциями экрана (клиент присылает при входе).
/// </summary>
public interface IPlayerViewSource
{
    /// <summary>Этим игроком управляют с этого компьютера (у него здесь своя камера).</summary>
    bool IsLocalPlayer { get; }
    /// <summary>Данные взгляда известны (на сервере — да).</summary>
    bool HasView { get; }
    Vector3 EyePosition { get; }
    Quaternion EyeRotation { get; }
    /// <summary>Вертикальный угол обзора, градусы (как Camera.fieldOfView).</summary>
    float FieldOfView { get; }
    /// <summary>Ширина экрана / высота.</summary>
    float Aspect { get; }
}

/// <summary>Взгляд игрока в один момент: откуда, куда, какой кадр.</summary>
public struct PlayerView
{
    public Vector3 Eye;
    public Quaternion Rotation;
    public float FieldOfView;
    public float Aspect;
    /// <summary>Взгляд пришёл по сети (с задержкой), а не с камеры этого компьютера.</summary>
    public bool IsRemote;

    public static PlayerView FromCamera(Camera camera) => new PlayerView
    {
        Eye = camera.transform.position,
        Rotation = camera.transform.rotation,
        FieldOfView = camera.fieldOfView,
        Aspect = camera.aspect,
        IsRemote = false
    };
}
