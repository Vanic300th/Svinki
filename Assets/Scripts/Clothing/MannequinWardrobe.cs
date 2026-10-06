using System.Collections.Generic;
using UnityEngine;

/// <summary>Live portrait of the local pig, with its own complete mesh and the player's current pose.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(300)]
public sealed class MannequinWardrobe : MonoBehaviour
{
    [SerializeField] private Transform mannequinRoot;
    [Tooltip("Чей комплект показывать. Пусто = свой игрок в этой сцене (в онлайне привязывает NetworkPlayer)")]
    [SerializeField] private PlayerOutfit outfit;
    [Tooltip("Сколько секунд висит подсказка «Воришка украл: …»")]
    [SerializeField, Min(0f)] private float messageTime = 2.5f;

    private readonly Dictionary<ClothingSlot, GameObject> equipped = new Dictionary<ClothingSlot, GameObject>();
    private PlayerOutfit bound;
    [SerializeField] private GameHud hud;
    private PigAppearance preview, source;
    private Camera portraitCamera;
    private readonly List<Transform> framingBones = new List<Transform>();
    private Vector3 framingCenter;
    private float framingDistance = 4.8f;
    private readonly List<(Transform source, Transform target)> poseLinks = new List<(Transform, Transform)>();

    private void Awake() => EnsurePreview();

    private void EnsurePreview()
    {
        if (preview != null || mannequinRoot == null) return;
        preview = mannequinRoot.GetComponentInChildren<PigAppearance>(true);
        if (preview == null)
        {
            foreach (Renderer old in mannequinRoot.GetComponentsInChildren<Renderer>(true)) old.enabled = false;
            foreach (Animator old in mannequinRoot.GetComponentsInChildren<Animator>(true)) old.enabled = false;
            GameObject prefab = Resources.Load<GameObject>("Pigs/PigAvatar");
            if (prefab == null) return;
            preview = Instantiate(prefab, mannequinRoot).GetComponent<PigAppearance>();
        }
        foreach (PigMotion motion in preview.GetComponentsInChildren<PigMotion>(true)) motion.enabled = false;
        foreach (Animator animator in preview.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (Transform part in preview.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 5;
        foreach (SkinnedMeshRenderer skin in preview.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
        PrepareModel(preview.gameObject, null);
        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (camera.gameObject.scene == gameObject.scene && camera.name == "HUD Character Camera") portraitCamera = camera;
        foreach (Transform part in preview.ModelRoot.GetComponentsInChildren<Transform>(true))
            if (part.name == "Head" || part.name == "Hips" || part.name.StartsWith("Hand_") || part.name.StartsWith("Foot_")) framingBones.Add(part);
        framingCenter = mannequinRoot.position + Vector3.up * .95f;
    }

    private void LinkPose()
    {
        poseLinks.Clear();
        source = bound != null ? bound.GetComponentInChildren<PigAppearance>(true) : null;
        if (source == null || preview == null) return;
        // Capture the base rig before adding garments: each garment also contains an unused rig.
        var sourceBones = new Dictionary<string, Transform>();
        foreach (Transform part in source.ModelRoot.GetComponentsInChildren<Transform>(true))
            if (!sourceBones.ContainsKey(part.name)) sourceBones.Add(part.name, part);
        foreach (Transform part in preview.ModelRoot.GetComponentsInChildren<Transform>(true))
            if (sourceBones.TryGetValue(part.name, out Transform from)) poseLinks.Add((from, part));
    }

    private void LateUpdate()
    {
        if (bound == null)
        {
            PlayerOutfit local = FindLocalOutfit();
            if (local != null) Bind(local);
        }
        if (source == null || preview == null) return;
        if (source.FaceCode != preview.FaceCode) preview.Apply(source.FaceCode);
        preview.ModelRoot.localPosition = source.ModelRoot.localPosition;
        preview.ModelRoot.localRotation = source.ModelRoot.localRotation;
        preview.ModelRoot.localScale = source.ModelRoot.localScale;
        foreach (var link in poseLinks)
        {
            if (link.source == null || link.target == null) continue;
            link.target.SetLocalPositionAndRotation(link.source.localPosition, link.source.localRotation);
            link.target.localScale = link.source.localScale;
        }
        FramePortrait();
    }

    private void FramePortrait()
    {
        if (portraitCamera == null || framingBones.Count == 0) return;
        // Follow the pose without moving the pig out of its studio, including a sideways fall.
        Bounds pose = new Bounds(framingBones[0].position, Vector3.one * .36f);
        foreach (Transform bone in framingBones)
        {
            pose.Encapsulate(new Bounds(bone.position, Vector3.one * .36f));
            if (bone.name == "Head") pose.Encapsulate(new Bounds(bone.position + bone.up * .2f, Vector3.one * .8f));
        }
        Vector3 direction = new Vector3(.55f, .1f, 4.8f).normalized;
        Quaternion rotation = Quaternion.LookRotation(-direction);
        Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
        Vector3 e = pose.extents;
        float Project(Vector3 axis) => Mathf.Abs(axis.x) * e.x + Mathf.Abs(axis.y) * e.y + Mathf.Abs(axis.z) * e.z;
        float tangent = Mathf.Tan(portraitCamera.fieldOfView * .5f * Mathf.Deg2Rad);
        float distance = Mathf.Max(4.8f, Project(up) / tangent, Project(right) / (tangent * portraitCamera.aspect)) + .35f;
        float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);
        framingCenter = Vector3.Lerp(framingCenter, pose.center, blend);
        framingDistance = Mathf.Lerp(framingDistance, distance, blend);
        portraitCamera.transform.SetPositionAndRotation(framingCenter + direction * framingDistance, rotation);
    }

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

        preview?.ModelRoot.GetComponent<PigClothingMask>()?.Restore();
        bound = value;
        EnsurePreview();
        LinkPose();
        if (bound == null) return;
        bound.Added += Equip;
        bound.Removed += HandleRemoved;
        foreach (ClothingDefinition clothing in bound.Items) Equip(clothing);
    }

    public void Unequip(ClothingSlot slot)
    {
        if (!equipped.TryGetValue(slot, out GameObject old)) return;
        if (old != null) { old.SetActive(false); Destroy(old); }
        equipped.Remove(slot);
        if (slot == ClothingSlot.Legs) preview?.ModelRoot.GetComponent<PigClothingMask>()?.Restore();
    }

    /// <summary>Подсказка сверху экрана на пару секунд («Воришка украл: Шапка»).</summary>
    public void ShowMessage(string text)
    {
        if (hud == null) hud = GameHud.Find(this);
        if (hud != null) hud.ShowNotification(text, messageTime);
    }

    private void HandleRemoved(ClothingDefinition clothing, string reason)
    {
        if (clothing == null) return;
        Unequip(clothing.Slot);
        if (!string.IsNullOrEmpty(reason)) ShowMessage(reason + ": " + clothing.DisplayName);
    }

    public void Equip(ClothingDefinition clothing)
    {
        EnsurePreview();
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
        if (preview != null && PigClothingBinding.TryAttach(clothing, preview.ModelRoot, slotRoot.transform))
        {
            PrepareModel(slotRoot, null);
            return;
        }

        if (clothing is ShoesClothing shoes && !clothing.PigRigged)
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
        fabric.name = "Fabric";
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
