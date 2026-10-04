using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Пересобирает NavMesh при каждом запуске Play.
/// Для грейбокса: расставил кубы — нажал Play — манекены уже умеют их обходить, «Bake» жать не нужно.
/// В NavMesh не попадают:
///  • вещи для подбора — они висят в воздухе и не должны быть препятствием;
///  • декор без коллайдера с моделью без Read/Write (одежда на витрине и т.п.) — сквозь него и так можно пройти,
///    а прочитать такую модель в собранной игре нельзя (в редакторе это сыпало ошибками RuntimeNavMeshBuilder).
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
        IgnoreUnreadableDecor();
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

    private void IgnoreUnreadableDecor()
    {
        foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
        {
            if (filter.gameObject.scene != gameObject.scene) continue;
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || mesh.isReadable) continue;        // читаемые модели NavMesh берёт без ошибок
            if (filter.TryGetComponent(out Collider _)) continue;  // с коллайдером — это стена/пол, оставляем
            if (filter.TryGetComponent(out NavMeshModifier modifier)) continue;
            modifier = filter.gameObject.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = false;
        }
    }
}
