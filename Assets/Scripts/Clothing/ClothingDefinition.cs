using UnityEngine;

public enum ClothingSlot { Head, Torso, Legs, Feet }

public abstract class ClothingDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Новая одежда";
    [SerializeField] private GameObject model;
    [SerializeField] private Material fabricMaterial;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Vector3 displayCenter;
    [SerializeField] private Vector2 displaySize = Vector2.one;
    [SerializeField] private Vector3 displayRotation;

    public abstract ClothingSlot Slot { get; }
    public string DisplayName => displayName;
    public GameObject Model => model;
    public Material FabricMaterial => fabricMaterial;
    public Material OutlineMaterial => outlineMaterial;
    public Color HighlightColor => highlightColor;
    public Vector3 DisplayCenter => displayCenter;
    public Vector2 DisplaySize => displaySize;
    public Vector3 DisplayRotation => displayRotation;
}
