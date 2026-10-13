using UnityEngine;

[CreateAssetMenu(fileName = "New Shirt", menuName = "Svinki/Одежда/Футболка")]
public sealed class ShirtClothing : ClothingDefinition
{
    public override ClothingSlot Slot => ClothingSlot.Torso;
}
