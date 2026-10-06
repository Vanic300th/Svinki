using System.Linq;
using UnityEngine;

/// <summary>Hides skin beneath trousers and restores the complete pig when they are removed.</summary>
public sealed class PigClothingMask : MonoBehaviour
{
    private SkinnedMeshRenderer body;
    private Mesh original, covered;
    public void CoverLegs(SkinnedMeshRenderer trousers)
    {
        if (body == null) body = GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == "Body");
        if (body == null || trousers == null) return;
        if (original == null) original = body.sharedMesh;
        Restore();
        var rendered = trousers.sharedMesh.triangles.Distinct().ToArray();
        var pantsPoints = trousers.sharedMesh.vertices;
        Matrix4x4 pantsToModel = transform.worldToLocalMatrix * trousers.transform.localToWorldMatrix;
        float waist = rendered.Max(i => pantsToModel.MultiplyPoint3x4(pantsPoints[i]).y) - .012f;
        float hem = rendered.Min(i => pantsToModel.MultiplyPoint3x4(pantsPoints[i]).y) + .005f;
        var vertices = original.vertices; var weights = original.boneWeights;
        Matrix4x4 bodyToModel = transform.worldToLocalMatrix * body.transform.localToWorldMatrix;
        bool Covered(int index)
        {
            var w = weights[index];
            float arm = Arm(w.boneIndex0) * w.weight0 + Arm(w.boneIndex1) * w.weight1 + Arm(w.boneIndex2) * w.weight2 + Arm(w.boneIndex3) * w.weight3;
            float height = bodyToModel.MultiplyPoint3x4(vertices[index]).y;
            return arm < .12f && height < waist && height > hem;
        }
        float Arm(int index) => body.bones[index].name.Contains("Arm") || body.bones[index].name.Contains("Forearm") || body.bones[index].name.Contains("Hand") ? 1 : 0;
        covered = Instantiate(original); covered.name = "Pig skin under trousers";
        for (int sub = 0; sub < original.subMeshCount; sub++)
        {
            int[] source = original.GetTriangles(sub);
            var kept = new System.Collections.Generic.List<int>();
            for (int i = 0; i < source.Length; i += 3)
                if (!(Covered(source[i]) && Covered(source[i + 1]) && Covered(source[i + 2])))
                { kept.Add(source[i]); kept.Add(source[i + 1]); kept.Add(source[i + 2]); }
            covered.SetTriangles(kept, sub);
        }
        covered.bounds = original.bounds; body.sharedMesh = covered;
    }
    public void Restore()
    {
        if (body != null && original != null) body.sharedMesh = original;
        if (covered != null) { ReleaseMesh(); covered = null; }
    }
    private void ReleaseMesh() { if (Application.isPlaying) Destroy(covered); else DestroyImmediate(covered); }
    private void OnDestroy() { if (covered != null) ReleaseMesh(); }
}
