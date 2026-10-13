using UnityEngine;

[RequireComponent(typeof(PickupItem))]
public sealed class PhotoCameraPickup : MonoBehaviour
{
    private void OnEnable() => GetComponent<PickupItem>().PickedUp += Collected;
    private void OnDisable() => GetComponent<PickupItem>().PickedUp -= Collected;
    private void Collected(PickupItem item)
    {
        if (item.Picker == null) return;
        var avatar = PlayerRegistry.FromObject(item.Picker);
        if (avatar != null && avatar.IsLocal) avatar.GetComponent<PlayerPhotoCamera>()?.Grant();
    }
}
