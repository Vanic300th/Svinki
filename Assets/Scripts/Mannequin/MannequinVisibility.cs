using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Отвечает на один вопрос: видит ли этого манекена хоть один игрок его комнаты прямо сейчас.
/// Для каждого игрока (PlayerRegistry, только своя сцена):
/// 1) Точка тела попадает в кадр его глаз (с небольшим запасом по краям).
/// 2) Луч от глаз до точки не упирается в стену — в физике своей сцены
///    (в онлайне у каждой комнаты на сервере физика отдельная).
/// Свой игрок проверяется по камере, чужой на сервере — по взгляду, который пришёл по сети.
/// Отдельный слой для стен не нужен: обзор закрывает ЛЮБОЙ коллайдер,
/// кроме слоя Ignore Raycast (на нём игрок) и самого манекена.
/// </summary>
[DefaultExecutionOrder(250)] // после камеры, чтобы проверять актуальный поворот взгляда
public class MannequinVisibility : MonoBehaviour
{
    [Header("Кто смотрит")]
    [Tooltip("Только для отладки: проверять одной этой камерой. Пусто = все игроки комнаты")]
    [SerializeField] private Camera observerCamera;

    [Header("Точки на теле (кости)")]
    [SerializeField] private Transform[] sightPoints;

    [Header("Настройки")]
    [Tooltip("Дальше этой дистанции манекен считается невидимым")]
    [SerializeField] private float maxViewDistance = 60f;
    [Tooltip("Запас за краем экрана (0.03 = 3%). Край тела у рамки кадра уже считается увиденным")]
    [SerializeField] private float screenMargin = 0.03f;
    [Tooltip("Запас для игроков, чей взгляд пришёл по сети: сервер узнаёт поворот с опозданием на пинг")]
    [SerializeField] private float networkScreenMargin = 0.1f;
    [Tooltip("Сколько секунд нужно не видеть манекена, чтобы он пошёл (убирает дёрганье на краю экрана)")]
    [SerializeField] private float unseenDelay = 0.2f;
    [Tooltip("Какие слои могут закрывать обзор. По умолчанию всё, кроме Ignore Raycast")]
    [SerializeField] private LayerMask occluderLayers = Physics.DefaultRaycastLayers;

    [Header("Отладка")]
    [SerializeField] private bool drawDebugLines = true;

    /// <summary>Итоговое значение с задержкой: манекена видит хоть один игрок.</summary>
    public bool IsSeen { get; private set; }
    /// <summary>Сырой результат этого кадра без задержки.</summary>
    public bool IsSeenRaw { get; private set; }
    /// <summary>Кто из игроков видит его в этом кадре (первый найденный). null — никто или проверка камерой.</summary>
    public PlayerAvatar SeenBy { get; private set; }
    /// <summary>Камера для поворота головы к игроку (своя камера этого компьютера).</summary>
    public Camera ObserverCamera => observerCamera != null ? observerCamera : Camera.main;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private readonly List<PlayerAvatar> roomPlayers = new List<PlayerAvatar>();
    private bool[] pointVisible;
    private float lastSeenTime = -999f;
    private Vector3 lastEye;
    private bool hasLastEye;

    private void Awake()
    {
        if (sightPoints == null || sightPoints.Length == 0) sightPoints = new[] { transform };
        pointVisible = new bool[sightPoints.Length];
    }

    private void LateUpdate()
    {
        IsSeenRaw = CheckAllObservers(out PlayerAvatar seenBy);
        SeenBy = seenBy;

        if (IsSeenRaw) lastSeenTime = Time.time;
        IsSeen = IsSeenRaw || Time.time - lastSeenTime < unseenDelay;
    }

    private bool CheckAllObservers(out PlayerAvatar seenBy)
    {
        seenBy = null;
        hasLastEye = false;

        // Отладка: одна заданная камера
        if (observerCamera != null)
            return CheckView(PlayerView.FromCamera(observerCamera), null);

        PlayerRegistry.GetPlayers(gameObject.scene, roomPlayers);
        if (roomPlayers.Count == 0 && PlayerRegistry.Players.Count == 0)
        {
            // Старые сцены без игроков в реестре — как раньше, главной камерой
            Camera main = Camera.main;
            return main != null && main.gameObject.scene == gameObject.scene && CheckView(PlayerView.FromCamera(main), null);
        }

        foreach (PlayerAvatar player in roomPlayers)
        {
            if (!player.TryGetView(out PlayerView view)) continue;
            if (!CheckView(view, player.transform)) continue;
            seenBy = player;
            return true; // хватит одного
        }
        return false;
    }

    /// <summary>Видна ли хоть одна точка тела этому взгляду. ignore — коллайдеры самого смотрящего.</summary>
    private bool CheckView(PlayerView view, Transform ignore)
    {
        Vector3 eye = view.Eye;
        lastEye = eye;
        hasLastEye = true;

        Quaternion toLocal = Quaternion.Inverse(view.Rotation);
        float tanHalfV = Mathf.Tan(view.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanHalfH = tanHalfV * view.Aspect;
        // Запас по краям: 0.03 ширины кадра с каждой стороны = границы шире на 2 × 0.03
        float limit = 1f + 2f * (view.IsRemote ? networkScreenMargin : screenMargin);
        PhysicsScene physics = gameObject.scene.GetPhysicsScene();

        bool anyVisible = false;
        for (int i = 0; i < sightPoints.Length; i++)
        {
            pointVisible[i] = false;
            Transform point = sightPoints[i];
            if (point == null) continue;

            Vector3 toTarget = point.position - eye;
            float distance = toTarget.magnitude;
            if (distance > maxViewDistance || distance < 0.001f) continue;

            // 1) В кадре ли точка
            Vector3 local = toLocal * toTarget;
            if (local.z <= 0.05f) continue;
            if (Mathf.Abs(local.x / local.z) > tanHalfH * limit ||
                Mathf.Abs(local.y / local.z) > tanHalfV * limit) continue;

            // 2) Не закрыта ли стеной
            if (IsBlocked(physics, eye, toTarget / distance, distance, ignore)) continue;

            pointVisible[i] = true;
            anyVisible = true;
        }
        return anyVisible;
    }

    private bool IsBlocked(PhysicsScene physics, Vector3 origin, Vector3 direction, float distance, Transform ignore)
    {
        int count = physics.Raycast(origin, direction, hits, distance - 0.05f,
            occluderLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = hits[i].collider.transform;
            // Собственные коллайдеры манекена и тело смотрящего обзор не закрывают
            if (hit.IsChildOf(transform)) continue;
            if (ignore != null && hit.IsChildOf(ignore)) continue;
            return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugLines || !Application.isPlaying || !hasLastEye || pointVisible == null) return;
        for (int i = 0; i < sightPoints.Length; i++)
        {
            if (sightPoints[i] == null) continue;
            Gizmos.color = pointVisible[i] ? Color.green : new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawLine(lastEye, sightPoints[i].position);
            Gizmos.DrawSphere(sightPoints[i].position, 0.04f);
        }
    }
}
