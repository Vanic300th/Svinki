using UnityEngine;

/// <summary>
/// Отвечает на один вопрос: видит ли игрок этого манекена прямо сейчас.
/// 1) Точка тела попадает в кадр камеры (с небольшим запасом по краям).
/// 2) Луч от камеры до точки не упирается в стену.
/// Отдельный слой для стен не нужен: обзор закрывает ЛЮБОЙ коллайдер,
/// кроме слоя Ignore Raycast (на нём игрок) и самого манекена.
/// </summary>
[DefaultExecutionOrder(100)] // после камеры, чтобы проверять актуальный поворот взгляда
public class MannequinVisibility : MonoBehaviour
{
    [Header("Кто смотрит")]
    [Tooltip("Пусто = Main Camera")]
    [SerializeField] private Camera observerCamera;

    [Header("Точки на теле (кости)")]
    [SerializeField] private Transform[] sightPoints;

    [Header("Настройки")]
    [Tooltip("Дальше этой дистанции манекен считается невидимым")]
    [SerializeField] private float maxViewDistance = 60f;
    [Tooltip("Запас за краем экрана (0.03 = 3%). Край тела у рамки кадра уже считается увиденным")]
    [SerializeField] private float screenMargin = 0.03f;
    [Tooltip("Сколько секунд нужно не видеть манекена, чтобы он пошёл (убирает дёрганье на краю экрана)")]
    [SerializeField] private float unseenDelay = 0.2f;
    [Tooltip("Какие слои могут закрывать обзор. По умолчанию всё, кроме Ignore Raycast")]
    [SerializeField] private LayerMask occluderLayers = Physics.DefaultRaycastLayers;

    [Header("Отладка")]
    [SerializeField] private bool drawDebugLines = true;

    /// <summary>Итоговое значение с задержкой: игрок видит манекена.</summary>
    public bool IsSeen { get; private set; }
    /// <summary>Сырой результат этого кадра без задержки.</summary>
    public bool IsSeenRaw { get; private set; }
    public Camera ObserverCamera => observerCamera;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private bool[] pointVisible;
    private float lastSeenTime = -999f;

    private void Awake()
    {
        if (observerCamera == null) observerCamera = Camera.main;
        if (sightPoints == null || sightPoints.Length == 0) sightPoints = new[] { transform };
        pointVisible = new bool[sightPoints.Length];
    }

    private void LateUpdate()
    {
        if (observerCamera == null) observerCamera = Camera.main;
        IsSeenRaw = observerCamera != null && CheckVisible(observerCamera);

        if (IsSeenRaw) lastSeenTime = Time.time;
        IsSeen = IsSeenRaw || Time.time - lastSeenTime < unseenDelay;
    }

    private bool CheckVisible(Camera cam)
    {
        Vector3 eye = cam.transform.position;
        bool anyVisible = false;

        for (int i = 0; i < sightPoints.Length; i++)
        {
            pointVisible[i] = false;
            Transform point = sightPoints[i];
            if (point == null) continue;

            Vector3 target = point.position;
            Vector3 toTarget = target - eye;
            float distance = toTarget.magnitude;
            if (distance > maxViewDistance || distance < 0.001f) continue;

            // 1) В кадре ли точка
            Vector3 vp = cam.WorldToViewportPoint(target);
            if (vp.z <= cam.nearClipPlane) continue;
            if (vp.x < -screenMargin || vp.x > 1f + screenMargin ||
                vp.y < -screenMargin || vp.y > 1f + screenMargin) continue;

            // 2) Не закрыта ли стеной
            if (IsBlocked(eye, toTarget / distance, distance)) continue;

            pointVisible[i] = true;
            anyVisible = true;
        }
        return anyVisible;
    }

    private bool IsBlocked(Vector3 origin, Vector3 direction, float distance)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance - 0.05f,
            occluderLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            // Собственные коллайдеры манекена обзор не закрывают
            if (hits[i].collider.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugLines || !Application.isPlaying || observerCamera == null || pointVisible == null) return;
        Vector3 eye = observerCamera.transform.position;
        for (int i = 0; i < sightPoints.Length; i++)
        {
            if (sightPoints[i] == null) continue;
            Gizmos.color = pointVisible[i] ? Color.green : new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawLine(eye, sightPoints[i].position);
            Gizmos.DrawSphere(sightPoints[i].position, 0.04f);
        }
    }
}
