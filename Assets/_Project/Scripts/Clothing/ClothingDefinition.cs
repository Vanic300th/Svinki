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
    [SerializeField, Tooltip("Необязательная картинка для инвентаря. Если не задана, меню показывает изображение 3D-модели.")]
    private Sprite inventoryIcon;
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
    public Sprite InventoryIcon => inventoryIcon;
    public float FlutterStrength => flutterStrength;

    public static string ReadableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Item";
        if (name.EndsWith("_worn", System.StringComparison.Ordinal))
            name = name.Substring(0, name.Length - "_worn".Length);
        switch (name)
        {
            case "hat1": return "Knitted hat";
            case "hat2": return "Light hat";
            case "cap1": return "Cap";
            case "shirt1": return "Collared shirt";
            case "shirt2": return "Dark T-shirt";
            case "shirt3": return "Colourful T-shirt";
            case "hoodie1": return "Hoodie";
            case "sleeve1": return "Camouflage long sleeve";
            case "sweater1": return "Warm sweater";
            case "pants1": return "Light trousers";
            case "pants2": return "Jeans";
            case "pants3": return "Patterned trousers";
            case "loafers1": return "Leather loafers";
            case "sneakers2": return "Lace-up sneakers";
            case "sneakers3": return "Sport sneakers";
            case "cap2": return "Woven cap";
            case "hat3": return "Speckled hat";
            case "shirt4": return "Cream floral T-shirt";
            case "shirt5": return "Tiger T-shirt";
            case "shirt6": return "Yellow floral T-shirt";
            case "shirt7": return "Sakura T-shirt";
            case "hoodie4": return "Grey camouflage hoodie";
            case "hoodie5": return "Brown patterned hoodie";
            case "sweater2": return "Blue sweater";
            case "sweater4": return "Autumn sweater";
            case "sweater5": return "Checked sweater";
            case "sweater6": return "Floral sweater";
            case "pants6": return "Golden trousers";
            case "pants7": return "Grey camouflage trousers";
            case "pants8": return "Autumn camouflage trousers";
            case "pants9": return "Pink dotted trousers";
            case "loafers2": return "Patterned loafers";
            case "sneakers4": return "Speckled sneakers";
            default: return name;
        }
    }
}
