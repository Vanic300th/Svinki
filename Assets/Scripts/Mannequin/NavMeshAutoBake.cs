using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Пересобирает NavMesh при каждом запуске Play.
/// Для грейбокса: расставил кубы — нажал Play — манекены уже умеют их обходить, «Bake» жать не нужно.
/// Вещи для подбора (одежда) в NavMesh не попадают: они висят в воздухе и не должны быть препятствием,
/// а их модели не разрешают чтение из скрипта (в сборке игры это сломало бы сборку NavMesh).
/// </summary>
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(NavMeshSurface))]
public class NavMeshAutoBake : MonoBehaviour
{
    [SerializeField] private bool bakeOnPlay = true;

    private void Awake()
    {
        if (!bakeOnPlay) return;
        IgnorePickups();
        GetComponent<NavMeshSurface>().BuildNavMesh();
    }

    private void IgnorePickups()
    {
        foreach (PickupItem pickup in FindObjectsByType<PickupItem>(FindObjectsInactive.Include))
        {
            if (pickup.gameObject.scene != gameObject.scene) continue; // только своя комната
            if (pickup.TryGetComponent(out NavMeshModifier modifier)) continue;
            modifier = pickup.gameObject.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true; // вместе со всеми дочерними моделями
        }
    }
}
