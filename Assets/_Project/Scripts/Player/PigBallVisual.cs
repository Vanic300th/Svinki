using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A spherical pig with the player's actual face, accessories and coat markings.</summary>
public sealed class PigBallVisual : IDisposable
{
    private readonly Transform ball;
    private readonly Renderer coat;
    private readonly Mesh sphere;
    private readonly List<Mesh> features = new List<Mesh>();
    private readonly MaterialPropertyBlock colors = new MaterialPropertyBlock();
    private GameObject faceRoot;
    private long appearance = -1;

    public PigBallVisual(Transform ball)
    {
        this.ball = ball;
        coat = ball.GetComponent<Renderer>();
        sphere = UnityEngine.Object.Instantiate(ball.GetComponent<MeshFilter>().sharedMesh);
        sphere.name = "Pig ball coat";
        var rest = new List<Vector3>();
        foreach (var point in sphere.vertices) rest.Add(new Vector3(point.x, -point.z, point.y + 1.43f));
        sphere.SetUVs(3, rest);
        ball.GetComponent<MeshFilter>().sharedMesh = sphere;
    }

    public void Apply(long code)
    {
        code = PigFace.Sanitize(code);
        if (code == appearance) return;
        var prefab = Resources.Load<GameObject>("Pigs/PigAvatar");
        if (prefab == null) return;
        ClearFace();
        // Bake at unit scale: BakeMesh includes inherited scale even for an unscaled renderer.
        var model = UnityEngine.Object.Instantiate(prefab);
        model.name = "Ball face source";
        try
        {
            foreach (var motion in model.GetComponentsInChildren<PigMotion>(true)) motion.enabled = false;
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var pig = model.GetComponent<PigAppearance>();
            pig.Apply(code);
            faceRoot = new GameObject("Pig ball identity");
            faceRoot.transform.SetParent(ball, false);
            // The original head is an ellipsoid centred 30 cm above its bone pivot.
            Vector3 center = new Vector3(0, 1.43f, 0);
            foreach (Transform bone in pig.ModelRoot.GetComponentsInChildren<Transform>(true))
                if (bone.name == "Head") { center = model.transform.InverseTransformPoint(bone.position) + Vector3.up * .30f; break; }
            foreach (var source in pig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!source.enabled || !source.gameObject.activeInHierarchy) continue;
                if (source.name == "Body")
                {
                    coat.sharedMaterial = source.sharedMaterial;
                    source.GetPropertyBlock(colors, 0);
                    coat.SetPropertyBlock(colors, 0);
                    continue;
                }
                if (source.name.StartsWith("Hoof_", StringComparison.Ordinal)) continue;
                var mesh = new Mesh { name = "Ball " + source.name };
                source.BakeMesh(mesh);
                // Bake in the avatar's neutral space, independent of the player's current animation.
                Matrix4x4 mapping = Matrix4x4.Scale(new Vector3(1.2f, 1.2f, 1.5f)) *
                    Matrix4x4.Translate(-center) * model.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                Matrix4x4 normalMatrix = mapping.inverse.transpose;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = mapping.MultiplyPoint3x4(vertices[i]);
                for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds();
                features.Add(mesh);
                var part = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(faceRoot.transform, false);
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                for (int i = 0; i < source.sharedMaterials.Length; i++)
                {
                    source.GetPropertyBlock(colors, i);
                    renderer.SetPropertyBlock(colors, i);
                }
            }
            appearance = code;
        }
        finally { model.SetActive(false); UnityEngine.Object.Destroy(model); }
    }

    private void ClearFace()
    {
        if (faceRoot != null) { faceRoot.SetActive(false); UnityEngine.Object.Destroy(faceRoot); }
        foreach (var mesh in features) if (mesh != null) UnityEngine.Object.Destroy(mesh);
        features.Clear();
    }

    public void Dispose()
    {
        ClearFace();
        if (sphere != null) UnityEngine.Object.Destroy(sphere);
    }
}
