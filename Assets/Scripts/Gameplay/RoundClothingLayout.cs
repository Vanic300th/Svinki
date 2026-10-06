using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Only the host/offline loader shuffles. NetworkPickup replicates the resulting positions.</summary>
public sealed class RoundClothingLayout : MonoBehaviour
{
    [SerializeField] private ClothingPickup[] pickups = Array.Empty<ClothingPickup>();
    [SerializeField] private Vector3[] positions = Array.Empty<Vector3>();
    [SerializeField] private Quaternion[] rotations = Array.Empty<Quaternion>();
    public int Seed { get; private set; }
    public bool Applied { get; private set; }
    public int Count => pickups.Length;

    public static void Begin(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var layout in root.GetComponentsInChildren<RoundClothingLayout>(true))
                if (!layout.Applied) layout.Shuffle(Guid.NewGuid().GetHashCode());
    }
    public void Shuffle(int seed)
    {
        if (NetworkLobby.Instance != null && !NetworkLobby.Instance.Offline && !NetworkLobby.Instance.IsHost) return;
        if (positions.Length != pickups.Length || rotations.Length != pickups.Length) return;
        Seed = seed;
        var order = Permutation(pickups.Length, seed);
        for (int i = 0; i < pickups.Length; i++)
            if (pickups[i] != null)
                pickups[i].transform.SetPositionAndRotation(positions[order[i]], rotations[order[i]]);
        Applied = true;
        Physics.SyncTransforms();
    }
    public static int[] Permutation(int count, int seed)
    {
        var order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;
        var random = new System.Random(seed);
        for (int i = count - 1; i > 0; i--)
        { int j = random.Next(i + 1); int swap = order[i]; order[i] = order[j]; order[j] = swap; }
        // Avoid an unchanged layout, even for a tiny map.
        if (count > 1 && Array.TrueForAll(order, i => order[i] == i))
        { int first = order[0]; order[0] = order[1]; order[1] = first; }
        return order;
    }
}
