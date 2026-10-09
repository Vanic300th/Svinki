using UnityEngine;

[RequireComponent(typeof(PickupItem))]
public sealed class PhotoCameraPickup : MonoBehaviour
{
    private void Awake() => GetComponent<PickupItem>().PickedUp += Collected;
    private void Collected(PickupItem item)
    {
        if (item.Picker == null) return;
        foreach (var avatar in PlayerRegistry.Players)
            if (avatar != null && avatar.IsLocal) { avatar.GetComponent<PlayerPhotoCamera>()?.Grant(); break; }
    }
}
