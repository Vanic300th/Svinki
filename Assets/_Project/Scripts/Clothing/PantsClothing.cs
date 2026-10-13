using UnityEngine;

[CreateAssetMenu(fileName = "New Pants", menuName = "Svinki/Одежда/Джинсы")]
public sealed class PantsClothing : ClothingDefinition
{
    public override ClothingSlot Slot => ClothingSlot.Legs;
}
