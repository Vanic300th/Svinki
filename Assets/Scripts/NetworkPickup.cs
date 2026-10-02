using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(PickupItem))]
public sealed class NetworkPickup : NetworkBehaviour
{
    private bool collected;

    public void TryCollect(NetworkPlayer player)
    {
        if (!IsServerStarted || collected || player == null) return;
        collected = true;
        ClothingPickup clothing = GetComponent<ClothingPickup>();
        if (clothing != null) player.AwardClothing(clothing.Clothing);
        NetworkManager.ServerManager.Despawn(NetworkObject);
    }
}
