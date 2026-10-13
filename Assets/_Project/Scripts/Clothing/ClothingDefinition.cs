using UnityEngine;

public enum ClothingSlot { Head, Torso, Legs, Feet }

public abstract class ClothingDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "New clothing";
    [SerializeField] private GameObject model;
    [SerializeField] private Material fabricMaterial;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Vector3 displayCenter;
    [SerializeField] private Vector2 displaySize = Vector2.one;
    [SerializeField] private Vector3 displayRotation;
    [SerializeField, Tooltip("Модель уже подогнана и привязана к скелету Pig_Rig; обувь содержит обе стопы.")]
    private bool pigRigged;
    [SerializeField] private GameObject pigWornModel;
    [SerializeField] private GameObject pickupPrefab;
    [SerializeField, Range(0f, 0.15f), Tooltip("Насколько колышется мягкая одежда на персонаже. 0 отключает эффект.")]
    private float flutterStrength = 0.04f;

    public abstract ClothingSlot Slot { get; }
    public string DisplayName => displayName;
    public GameObject Model => model;
    public Material FabricMaterial => fabricMaterial;
    public Material OutlineMaterial => outlineMaterial;
    public Color HighlightColor => highlightColor;
    public Vector3 DisplayCenter => displayCenter;
    public Vector2 DisplaySize => displaySize;
    public Vector3 DisplayRotation => displayRotation;
    public bool PigRigged => pigRigged;
    public GameObject WornModel => pigWornModel != null ? pigWornModel : model;
    public GameObject PickupPrefab => pickupPrefab;
    public float FlutterStrength => flutterStrength;
}
