using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Показывает одежду прямо на сетевом персонаже. PlayerOutfit получает изменения от сервера.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(PlayerOutfit))]
public sealed class WorldOutfitRenderer : MonoBehaviour
{
    [SerializeField] private Transform characterVisual;

    private readonly Dictionary<ClothingSlot, GameObject> equipped = new Dictionary<ClothingSlot, GameObject>();
    private PlayerOutfit outfit;
    private bool firstPersonHidden;

    private void Start()
    {
        outfit = GetComponent<PlayerOutfit>();
        outfit.Added += Equip;
        outfit.Removed += Unequip;
        foreach (ClothingDefinition clothing in outfit.Items) Equip(clothing);
        ApplyVisibility();
    }

    private void OnDestroy()
    {
        if (outfit == null) return;
        outfit.Added -= Equip;
        outfit.Removed -= Unequip;
    }

    public void SetFirstPersonHidden(bool hidden)
    {
        firstPersonHidden = hidden;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        int mirrorLayer = LayerMask.NameToLayer("LocalPlayerMirror");
        int visualLayer = firstPersonHidden && mirrorLayer >= 0 ? mirrorLayer : gameObject.layer;
        if (characterVisual != null) SetLayerRecursively(characterVisual, visualLayer);
        foreach (GameObject item in equipped.Values)
            if (item != null)
                SetLayerRecursively(item.transform, visualLayer);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            part.gameObject.layer = layer;
    }

    private void Unequip(ClothingDefinition clothing, string reason)
    {
        if (clothing == null || !equipped.TryGetValue(clothing.Slot, out GameObject old)) return;
        equipped.Remove(clothing.Slot);
        if (old != null) Destroy(old);
    }

    private void Equip(ClothingDefinition clothing)
    {
        if (clothing == null || clothing.Model == null) return;
        if (equipped.TryGetValue(clothing.Slot, out GameObject old) && old != null) Destroy(old);

        GameObject slot = new GameObject("Worn " + clothing.Slot + " - " + clothing.DisplayName);
        slot.transform.SetParent(transform, false);
        slot.layer = gameObject.layer;
        equipped[clothing.Slot] = slot;

        Animator animator = characterVisual != null ? characterVisual.GetComponentInChildren<Animator>(true) : null;
        if (clothing is ShoesClothing shoes)
        {
            GameObject left = AddPiece(clothing, slot.transform,
                clothing.DisplayCenter + Vector3.left * shoes.PairSpacing * .5f, true);
            GameObject right = AddPiece(clothing, slot.transform,
                clothing.DisplayCenter + Vector3.right * shoes.PairSpacing * .5f, false);
            FollowBone(left, animator, HumanBodyBones.LeftFoot);
            FollowBone(right, animator, HumanBodyBones.RightFoot);
        }
        else
        {
            GameObject piece = AddPiece(clothing, slot.transform, clothing.DisplayCenter, false);
            HumanBodyBones bone = clothing.Slot == ClothingSlot.Head ? HumanBodyBones.Head :
                clothing.Slot == ClothingSlot.Torso ? HumanBodyBones.Chest : HumanBodyBones.Hips;
            FollowBone(piece, animator, bone);
        }
        ApplyVisibility();
    }

    private static void FollowBone(GameObject piece, Animator animator, HumanBodyBones bone)
    {
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman) return;
        Transform target = animator.GetBoneTransform(bone);
        if (target != null) piece.AddComponent<WornGarmentFollower>().Bind(target);
    }

    private static GameObject AddPiece(ClothingDefinition clothing, Transform parent, Vector3 center, bool mirror)
    {
        GameObject holder = new GameObject(clothing.DisplayName);
        holder.transform.SetParent(parent, false);
        GameObject fabric = Instantiate(clothing.Model, holder.transform);
        fabric.name = "Fabric";
        fabric.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(clothing.DisplayRotation));
        fabric.transform.localScale = Vector3.one;
        Prepare(fabric, clothing.FabricMaterial, parent.gameObject.layer);

        Bounds bounds = LocalBounds(fabric, holder.transform);
        float width = Mathf.Max(bounds.size.x, .001f);
        float height = Mathf.Max(bounds.size.y, .001f);
        float scale = Mathf.Min(clothing.DisplaySize.x / width, clothing.DisplaySize.y / height);
        holder.transform.localScale = new Vector3(mirror ? -scale : scale, scale, scale);
        holder.transform.localPosition = center - Vector3.Scale(bounds.center, holder.transform.localScale);

        if (clothing.OutlineMaterial != null)
        {
            GameObject outline = Instantiate(clothing.Model, holder.transform);
            outline.name = "Contour";
            outline.transform.SetLocalPositionAndRotation(Vector3.zero, fabric.transform.localRotation);
            outline.transform.localScale = Vector3.one;
            Prepare(outline, clothing.OutlineMaterial, parent.gameObject.layer);
            foreach (Renderer renderer in outline.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        AddFlutter(clothing, holder);
        return holder;
    }

    private static void AddFlutter(ClothingDefinition clothing, GameObject holder)
    {
        if (clothing.Slot != ClothingSlot.Torso && clothing.Slot != ClothingSlot.Legs) return;
        if (clothing.FlutterStrength <= 0f) return;
        holder.AddComponent<WornClothFlutter>().Initialize(clothing.FlutterStrength, holder.transform.root);
    }

    private static void Prepare(GameObject model, Material material, int layer)
    {
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        if (material == null) return;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    private static Bounds LocalBounds(GameObject model, Transform reference)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds bounds = new Bounds();
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
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
}
