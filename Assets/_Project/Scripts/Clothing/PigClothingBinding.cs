using System.Collections.Generic;
using UnityEngine;

/// <summary>Shares the pig's actual skeleton, including its squash and ragdoll pose.</summary>
public static class PigClothingBinding
{
    public static bool TryAttach(ClothingDefinition clothing, Transform pigModel, Transform holder)
    {
        if (clothing == null || !clothing.PigRigged || clothing.Model == null || pigModel == null) return false;
        var bones = new Dictionary<string, Transform>();
        foreach (Transform bone in pigModel.GetComponentsInChildren<Transform>(true))
            // Other equipped garments contain an unused copy of the source rig.
            if (!bones.ContainsKey(bone.name)) bones.Add(bone.name, bone);

        holder.SetParent(pigModel, false);
        holder.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        holder.localScale = Vector3.one;
        GameObject fabric = Object.Instantiate(clothing.WornModel, holder);
        fabric.name = "Pig fitted fabric";
        fabric.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        fabric.transform.localScale = Vector3.one;
        var skins = fabric.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skins.Length == 0) { Object.Destroy(fabric); return false; }
        foreach (SkinnedMeshRenderer skin in skins)
        {
            var source = skin.bones;
            var mapped = new Transform[source.Length];
            for (int i = 0; i < source.Length; i++)
                if (source[i] == null || !bones.TryGetValue(source[i].name, out mapped[i]))
                {
                    Debug.LogWarning("Pig garment has an incompatible bone: " + clothing.DisplayName, holder);
                    Object.Destroy(fabric);
                    return false;
                }
            Transform root = skin.rootBone;
            skin.bones = mapped;
            if (root != null && bones.TryGetValue(root.name, out Transform mappedRoot)) skin.rootBone = mappedRoot;
            skin.updateWhenOffscreen = true;
        }
        foreach (Animator animator in fabric.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        ClothingVisuals.Prepare(fabric, clothing.FabricMaterial, holder.gameObject.layer);
        if (clothing.Slot == ClothingSlot.Legs)
        {
            var mask = pigModel.GetComponent<PigClothingMask>();
            if (mask == null) mask = pigModel.gameObject.AddComponent<PigClothingMask>();
            mask.CoverLegs(skins[0]);
        }
        return true;
    }
}
