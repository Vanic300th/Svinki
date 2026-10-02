using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(PickupItem))]
public sealed class ClothingPickup : MonoBehaviour
{
    [SerializeField] private ClothingDefinition clothing;
    [SerializeField] private MannequinWardrobe wardrobe;

    public ClothingDefinition Clothing => clothing;

    private void Reset()
    {
        wardrobe = FindAnyObjectByType<MannequinWardrobe>();
    }

    private void OnEnable()
    {
        GetComponent<PickupItem>().PickedUp += HandlePickup;
    }

    private void OnDisable()
    {
        PickupItem pickup = GetComponent<PickupItem>();
        if (pickup != null) pickup.PickedUp -= HandlePickup;
    }

    private void HandlePickup(PickupItem _)
    {
        // In multiplayer the server awards only the collecting player via NetworkPlayer.
        if (GetComponent<NetworkPickup>() != null) return;
        if (clothing == null)
        {
            Debug.LogWarning("Для предмета не назначена одежда.", this);
            return;
        }
        if (wardrobe == null) wardrobe = FindAnyObjectByType<MannequinWardrobe>();
        if (wardrobe != null) wardrobe.Equip(clothing);
        else Debug.LogWarning("Манекен для отображения одежды не найден.", this);
    }
}
