using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Список всех игроков. Манекены спрашивают игроков только отсюда, а не через Camera.main или поиск по сцене.
/// В онлайне на сервере каждая комната — отдельная копия сцены, поэтому манекены берут игроков
/// только своей сцены: GetPlayers(scene) / Nearest(..., scene).
/// </summary>
public static class PlayerRegistry
{
    private static readonly List<PlayerAvatar> players = new List<PlayerAvatar>();
    private static float nextFallbackTry;

    /// <summary>Все игроки во всех комнатах.</summary>
    public static IReadOnlyList<PlayerAvatar> Players
    {
        get
        {
            if (players.Count == 0) TryFallback();
            return players;
        }
    }

    public static void Register(PlayerAvatar player)
    {
        if (player != null && !players.Contains(player)) players.Add(player);
    }

    public static void Unregister(PlayerAvatar player) => players.Remove(player);

    /// <summary>Игроки одной комнаты (сцены). Список results очищается и заполняется заново.</summary>
    public static void GetPlayers(Scene scene, List<PlayerAvatar> results)
    {
        results.Clear();
        foreach (PlayerAvatar player in Players)
            if (player != null && player.IsAlive && player.gameObject.scene == scene)
                results.Add(player);
    }

    /// <summary>Ближайший игрок этой комнаты (по земле, без учёта высоты). null — никого ближе maxDistance.</summary>
    public static PlayerAvatar Nearest(Vector3 position, Scene scene, float maxDistance = float.PositiveInfinity)
    {
        PlayerAvatar best = null;
        float bestSqr = maxDistance * maxDistance;
        foreach (PlayerAvatar player in Players)
        {
            if (player == null || !player.IsAlive || player.gameObject.scene != scene) continue;
            Vector3 d = player.Position - position;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr <= bestSqr) { best = player; bestSqr = sqr; }
        }
        return best;
    }

    /// <summary>Какому игроку принадлежит объект: сам игрок, его ребёнок или его камера.</summary>
    public static PlayerAvatar FromObject(GameObject source)
    {
        if (source == null) return null;
        PlayerAvatar owner = source.GetComponentInParent<PlayerAvatar>();
        if (owner != null) return owner;
        foreach (PlayerAvatar player in Players)
        {
            if (player == null) continue;
            Camera camera = player.EyeCamera;
            if (camera != null && source.transform.IsChildOf(camera.transform)) return player;
        }
        return null;
    }

    /// <summary>
    /// Сцена ещё не настроена (на игроке нет PlayerAvatar) — добавляем его сами, чтобы всё работало.
    /// Сетевых игроков не трогаем: им PlayerAvatar добавляет NetworkPlayer.
    /// </summary>
    private static void TryFallback()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextFallbackTry) return;
        nextFallbackTry = Time.unscaledTime + 1f;

        GrayboxPlayerController controller = Object.FindAnyObjectByType<GrayboxPlayerController>();
        if (controller == null || controller.GetComponent<PlayerAvatar>() != null) return;
        if (controller.GetComponent<NetworkPlayer>() != null) return;
        controller.gameObject.AddComponent<PlayerAvatar>(); // сам запишется в реестр
        Debug.LogWarning("[Player] На игроке не было PlayerAvatar — добавил на время игры. " +
                         "Добавь его в сцену насовсем (Tools > Mannequin > Place Thief делает это сам).", controller);
    }

    // Если в настройках выключена перезагрузка домена, статический список переживает выход из Play — чистим
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        players.Clear();
        nextFallbackTry = 0f;
    }
}
