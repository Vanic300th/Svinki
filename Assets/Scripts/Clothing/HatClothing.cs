using UnityEngine;

[CreateAssetMenu(fileName = "New Hat", menuName = "Svinki/Одежда/Шапка")]
public sealed class HatClothing : ClothingDefinition
{
    public override ClothingSlot Slot => ClothingSlot.Head;
}
