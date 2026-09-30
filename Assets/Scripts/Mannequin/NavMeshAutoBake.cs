using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Пересобирает NavMesh при каждом запуске Play.
/// Для грейбокса: расставил кубы — нажал Play — манекены уже умеют их обходить, «Bake» жать не нужно.
/// </summary>
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(NavMeshSurface))]
public class NavMeshAutoBake : MonoBehaviour
{
    [SerializeField] private bool bakeOnPlay = true;

    private void Awake()
    {
        if (bakeOnPlay) GetComponent<NavMeshSurface>().BuildNavMesh();
    }
}
