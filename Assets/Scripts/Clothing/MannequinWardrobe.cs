using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD-манекен: показывает одежду из PlayerOutfit своего игрока. Сам ничего не хранит —
/// подписан на комплект и рисует то, что в нём есть (вещь добавили — надел, украли — снял).
/// В одиночке сам находит своего игрока; в онлайне его привязывает NetworkPlayer владельца.
/// </summary>
[DisallowMultipleComponent]
public sealed class MannequinWardrobe : MonoBehaviour
{
    [SerializeField] private Transform mannequinRoot;
    [Tooltip("Чей комплект показывать. Пусто = свой игрок в этой сцене (в онлайне привязывает NetworkPlayer)")]
    [SerializeField] private PlayerOutfit outfit;
    [Tooltip("Сколько секунд висит подсказка «Воришка украл: …»")]
    [SerializeField, Min(0f)] private float messageTime = 2.5f;

    private readonly Dictionary<ClothingSlot, GameObject> equipped = new Dictionary<ClothingSlot, GameObject>();
    private PlayerOutfit bound;
    private string message;
    private float messageUntil;
    private GUIStyle messageStyle;

    private void Start()
    {
        if (bound != null) return; // уже привязали снаружи
        if (outfit == null) outfit = FindLocalOutfit();
        if (outfit != null) Bind(outfit);
    }

    private PlayerOutfit FindLocalOutfit()
    {
        foreach (PlayerAvatar player in PlayerRegistry.Players)
            if (player != null && player.IsLocal && player.gameObject.scene == gameObject.scene)
                return player.Outfit;
        return null;
    }

    private void OnDestroy() => Bind(null);

    /// <summary>Показывать комплект другого игрока (в онлайне — свой у каждого клиента).</summary>
    public void Bind(PlayerOutfit value)
    {
        if (bound != null)
        {
            bound.Added -= Equip;
            bound.Removed -= HandleRemoved;
        }
        // Снимаем всё, что показывали для прошлого комплекта
        foreach (GameObject shown in equipped.Values)
            if (shown != null) Destroy(shown);
        equipped.Clear();

        bound = value;
        if (bound == null) return;
        bound.Added += Equip;
        bound.Removed += HandleRemoved;
        foreach (ClothingDefinition clothing in bound.Items) Equip(clothing);
    }

    public void Unequip(ClothingSlot slot)
    {
        if (!equipped.TryGetValue(slot, out GameObject old)) return;
        if (old != null) Destroy(old);
        equipped.Remove(slot);
    }

    /// <summary>Подсказка сверху экрана на пару секунд («Воришка украл: Шапка»).</summary>
    public void ShowMessage(string text)
    {
        message = text;
        messageUntil = Time.time + messageTime;
    }

    private void HandleRemoved(ClothingDefinition clothing, string reason)
    {
        if (clothing == null) return;
        Unequip(clothing.Slot);
        if (!string.IsNullOrEmpty(reason)) ShowMessage(reason + ": " + clothing.DisplayName);
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(message) || Time.time > messageUntil) return;
        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            messageStyle.normal.textColor = new Color(1f, 0.45f, 0.35f);
        }
        GUI.Box(new Rect(Screen.width * 0.5f - 170f, 60f, 340f, 38f), message, messageStyle);
    }

    public void Equip(ClothingDefinition clothing)
    {
        if (clothing == null || clothing.Model == null || mannequinRoot == null)
        {
            Debug.LogWarning("Проверь модель одежды и ссылку на манекен в гардеробе.", this);
            return;
        }

        if (equipped.TryGetValue(clothing.Slot, out GameObject old) && old != null)
            Destroy(old);

        GameObject slotRoot = new GameObject(clothing.Slot + " - " + clothing.DisplayName);
        slotRoot.layer = 5; // Только камера портрета (UI).
        slotRoot.transform.SetParent(mannequinRoot, false);
        equipped[clothing.Slot] = slotRoot;
        Transform movementSource = bound != null ? bound.transform : mannequinRoot;

        if (clothing is ShoesClothing shoes)
        {
            float halfGap = shoes.PairSpacing * 0.5f;
            AddPiece(clothing, slotRoot.transform, clothing.DisplayCenter + Vector3.left * halfGap, true, movementSource);
            AddPiece(clothing, slotRoot.transform, clothing.DisplayCenter + Vector3.right * halfGap, false, movementSource);
        }
        else AddPiece(clothing, slotRoot.transform, clothing.DisplayCenter, false, movementSource);
    }

    private static void AddPiece(ClothingDefinition clothing, Transform parent, Vector3 center, bool mirror,
        Transform movementSource)
    {
        GameObject holder = new GameObject(clothing.DisplayName + (mirror ? " L" : ""));
        holder.layer = 5;
        holder.transform.SetParent(parent, false);

        GameObject fabric = Instantiate(clothing.Model, holder.transform);
        fabric.name = "Одежда";
        fabric.transform.localPosition = Vector3.zero;
        fabric.transform.localRotation = Quaternion.Euler(clothing.DisplayRotation);
        fabric.transform.localScale = Vector3.one;
        PrepareModel(fabric, clothing.FabricMaterial);

        Bounds source = LocalBounds(fabric, holder.transform);
        float width = Mathf.Max(source.size.x, 0.001f);
        float height = Mathf.Max(source.size.y, 0.001f);
        float scale = Mathf.Min(clothing.DisplaySize.x / width, clothing.DisplaySize.y / height);
        float scaleX = mirror ? -scale : scale;
        holder.transform.localScale = new Vector3(scaleX, scale, scale);
        holder.transform.localPosition = center - Vector3.Scale(source.center, holder.transform.localScale);

        if ((clothing.Slot == ClothingSlot.Torso || clothing.Slot == ClothingSlot.Legs) &&
            clothing.FlutterStrength > 0f)
            holder.AddComponent<WornClothFlutter>().Initialize(clothing.FlutterStrength, movementSource);
    }

    private static void PrepareModel(GameObject model, Material material)
    {
        ClothingVisuals.Prepare(model, material, 5);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static Bounds LocalBounds(GameObject model, Transform reference)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 point = reference.InverseTransformPoint(new Vector3(
                    x == 0 ? world.min.x : world.max.x,
                    y == 0 ? world.min.y : world.max.y,
                    z == 0 ? world.min.z : world.max.z));
                if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }
}
