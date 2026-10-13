using UnityEngine;

[CreateAssetMenu(fileName = "New Hoodie", menuName = "Svinki/Одежда/Худи")]
public sealed class HoodieClothing : ClothingDefinition
{
    public override ClothingSlot Slot => ClothingSlot.Torso;
}
