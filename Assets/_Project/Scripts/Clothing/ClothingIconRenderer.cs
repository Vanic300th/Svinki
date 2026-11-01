using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>One-shot, isolated garment photos. Four inventory targets are reused until the view is destroyed.</summary>
public sealed class ClothingIconRenderer : IDisposable
{
    private const int Layer = 31;
    private readonly GameObject studio;
    private readonly Camera camera;

    public ClothingIconRenderer(Transform owner)
    {
        studio = new GameObject("Inventory clothing photo studio");
        studio.transform.SetParent(owner, false); studio.transform.position = new Vector3(30000, 30000, 30000);
        var cameraObject = new GameObject("Clothing icon camera"); cameraObject.transform.SetParent(studio.transform, false);
        camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << Layer; camera.nearClipPlane = .01f; camera.farClipPlane = 30;
        camera.allowHDR = false; camera.allowMSAA = false;
        var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderShadows = false; data.renderPostProcessing = false;
        AddLight("Clothing key light", new Vector3(-3, 4, 5), 65);
        AddLight("Clothing fill light", new Vector3(3, 1, 4), 35);
    }

    private void AddLight(string name, Vector3 position, float intensity)
    {
        var obj = new GameObject(name); obj.transform.SetParent(studio.transform, false);
        obj.transform.localPosition = position; obj.transform.LookAt(studio.transform.position);
        var light = obj.AddComponent<Light>(); light.type = LightType.Spot; light.range = 20;
        light.spotAngle = 100; light.intensity = intensity; light.cullingMask = 1 << Layer; light.shadows = LightShadows.None;
    }

    public bool Render(ClothingDefinition clothing, RenderTexture target)
    {
        if (clothing == null || clothing.Model == null) return false;
        GameObject model = null;
        var meshes = new List<Mesh>();
        try
        {
            model = UnityEngine.Object.Instantiate(clothing.WornModel, studio.transform);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, clothing.PigRigged ? Quaternion.identity : Quaternion.Euler(clothing.DisplayRotation));
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            ClothingVisuals.Prepare(model, clothing.FabricMaterial, Layer);
            // Rig bounds often cover the whole pig. Bake just the garment for a tight, stable thumbnail.
            foreach (var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skinned.enabled || skinned.sharedMesh == null) continue;
                var mesh = new Mesh { name = "Inventory baked garment" }; skinned.BakeMesh(mesh); meshes.Add(mesh);
                var piece = new GameObject("Baked icon mesh", typeof(MeshFilter), typeof(MeshRenderer));
                piece.layer = Layer; piece.transform.SetParent(skinned.transform, false);
                piece.GetComponent<MeshFilter>().sharedMesh = mesh;
                piece.GetComponent<MeshRenderer>().sharedMaterials = skinned.sharedMaterials;
                skinned.enabled = false;
            }
            bool found = false; Bounds bounds = default;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) return false;
            float extent = Mathf.Max(bounds.size.magnitude, .01f);
            model.transform.localScale /= extent;
            Vector3 center = studio.transform.position + (bounds.center - studio.transform.position) / extent;
            Vector3 size = bounds.size / extent;
            camera.aspect = (float)target.width / target.height;
            camera.orthographicSize = Mathf.Max(size.y, size.x / camera.aspect) * .62f + size.z * .04f;
            camera.transform.position = center + new Vector3(.15f, .08f, 3);
            camera.transform.LookAt(center); camera.targetTexture = target;
            if (GraphicsSettings.currentRenderPipeline == null) { camera.Render(); return true; }
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) return false;
            RenderPipeline.SubmitRenderRequest(camera, request); return true;
        }
        finally
        {
            camera.targetTexture = null;
            if (model != null) { model.SetActive(false); UnityEngine.Object.Destroy(model); }
            foreach (var mesh in meshes) UnityEngine.Object.Destroy(mesh);
        }
    }

    public void Dispose() { if (studio != null) UnityEngine.Object.Destroy(studio); }
}
