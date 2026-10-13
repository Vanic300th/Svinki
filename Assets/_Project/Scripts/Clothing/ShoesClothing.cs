using UnityEngine;

[CreateAssetMenu(fileName = "New Shoes", menuName = "Svinki/Одежда/Обувь")]
public sealed class ShoesClothing : ClothingDefinition
{
    [SerializeField, Min(0f)] private float pairSpacing = 0.25f;
    public override ClothingSlot Slot => ClothingSlot.Feet;
    public float PairSpacing => pairSpacing;
}
