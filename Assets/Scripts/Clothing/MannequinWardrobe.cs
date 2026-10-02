using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MannequinWardrobe : MonoBehaviour
{
    [SerializeField] private Transform mannequinRoot;
    private readonly Dictionary<ClothingSlot, GameObject> equipped = new Dictionary<ClothingSlot, GameObject>();

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

        if (clothing is ShoesClothing shoes)
        {
            float halfGap = shoes.PairSpacing * 0.5f;
            AddPiece(clothing, slotRoot.transform, clothing.DisplayCenter + Vector3.left * halfGap, true);
            AddPiece(clothing, slotRoot.transform, clothing.DisplayCenter + Vector3.right * halfGap, false);
        }
        else AddPiece(clothing, slotRoot.transform, clothing.DisplayCenter, false);
    }

    private static void AddPiece(ClothingDefinition clothing, Transform parent, Vector3 center, bool mirror)
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

        if (clothing.OutlineMaterial != null)
        {
            // Та же геометрия с раздуванием вершин вдоль нормалей:
            // край повторяет форму одежды, а не прямоугольник её границ.
            GameObject outline = Instantiate(clothing.Model, holder.transform);
            outline.name = "Силуэт";
            outline.transform.localPosition = fabric.transform.localPosition;
            outline.transform.localRotation = fabric.transform.localRotation;
            outline.transform.localScale = fabric.transform.localScale;
            PrepareModel(outline, clothing.OutlineMaterial);
        }
    }

    private static void PrepareModel(GameObject model, Material material)
    {
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
            part.gameObject.layer = 5;
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        if (material == null) return;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
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
