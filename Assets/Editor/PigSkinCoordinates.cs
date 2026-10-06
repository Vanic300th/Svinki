using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Stable marking coordinates shared by body, ears and the first-person mesh.</summary>
public sealed class PigSkinCoordinates : AssetPostprocessor
{
    private void OnPostprocessModel(GameObject root)
    {
        if (assetPath != "Assets/Art/Characters/Pigs/Pig_Modular.fbx") return;
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = renderer.sharedMesh;
            var coordinates = new List<Vector3>(mesh.vertexCount);
            Matrix4x4 toModel = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            foreach (Vector3 vertex in mesh.vertices) coordinates.Add(toModel.MultiplyPoint3x4(vertex));
            mesh.SetUVs(3, coordinates);
        }
    }
}
