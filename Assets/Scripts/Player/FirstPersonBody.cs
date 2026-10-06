using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

// A local headless skin shares the world character's bones; peers and mirrors keep the full model.
[DisallowMultipleComponent, DefaultExecutionOrder(200)]
public sealed class FirstPersonBody : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer source;
    [SerializeField] private Mesh bodyMesh;
    [SerializeField] private SkinnedMeshRenderer[] additionalSources;
    private NetworkPlayer player;
    private SkinnedMeshRenderer body;
    private readonly List<SkinnedMeshRenderer> additionalBodies = new List<SkinnedMeshRenderer>();
    private MaterialPropertyBlock appearanceColors;

    private void Awake() { player = GetComponent<NetworkPlayer>(); }

    private void LateUpdate()
    {
        bool local = player == null || player.IsOwner;
        if (!local || source == null || bodyMesh == null)
        {
            if (body != null) body.enabled = false;
            foreach (var part in additionalBodies) part.enabled = false;
            return;
        }
        if (body == null)
        {
            var visual = new GameObject("First person body");
            visual.transform.SetParent(transform, false);
            int layer = LayerMask.NameToLayer("FirstPersonBody");
            visual.layer = layer >= 0 ? layer : 0;
            body = visual.AddComponent<SkinnedMeshRenderer>();
            body.sharedMesh = bodyMesh;
            body.sharedMaterials = source.sharedMaterials;
            body.bones = source.bones;
            body.rootBone = source.rootBone;
            body.localBounds = source.localBounds;
            body.updateWhenOffscreen = true;
            body.shadowCastingMode = ShadowCastingMode.Off;
            if (additionalSources != null)
                foreach (var part in additionalSources)
                {
                    var extra = new GameObject("First person " + part.name);
                    extra.transform.SetParent(transform, false); extra.layer = visual.layer;
                    var skin = extra.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = part.sharedMesh; skin.sharedMaterials = part.sharedMaterials;
                    skin.bones = part.bones; skin.rootBone = part.rootBone; skin.localBounds = part.localBounds;
                    skin.updateWhenOffscreen = true; skin.shadowCastingMode = ShadowCastingMode.Off;
                    additionalBodies.Add(skin);
                }
        }
        body.enabled = true;
        Follow(body, source);
        for (int i = 0; i < additionalBodies.Count; i++)
        { additionalBodies[i].enabled = true; Follow(additionalBodies[i], additionalSources[i]); }
        Camera camera = Camera.main;
        if (camera != null) camera.cullingMask |= 1 << body.gameObject.layer;
    }
    private void Follow(SkinnedMeshRenderer skin, SkinnedMeshRenderer original)
    {
        if (appearanceColors == null) appearanceColors = new MaterialPropertyBlock();
        for (int i = 0; i < original.sharedMaterials.Length; i++)
        {
            original.GetPropertyBlock(appearanceColors, i);
            skin.SetPropertyBlock(appearanceColors, i);
        }
        skin.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
        skin.transform.localScale = new Vector3(
            original.transform.lossyScale.x / transform.lossyScale.x,
            original.transform.lossyScale.y / transform.lossyScale.y,
            original.transform.lossyScale.z / transform.lossyScale.z);
    }
}
