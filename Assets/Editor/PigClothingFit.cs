using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Transfers the pig skin weights and separates overlapping clothing surfaces.</summary>
public static class PigClothingFit
{
    private const string Root = "Assets/ClothingLibrary/Generated";
    private const string Meshes = Root + "/WearMeshes";
    private const string Models = Root + "/Wearables";
    private const float CellSize = .12f;

    public static string RebuildAll()
    {
        var definitions = AssetDatabase.FindAssets("", new[] { Root + "/Definitions" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ClothingDefinition>)
            .Where(c => c != null && c.PigRigged).ToArray();
        foreach (ClothingDefinition clothing in definitions) { Prepare(clothing, true); EditorUtility.SetDirty(clothing); }
        AssetDatabase.SaveAssets();
        return "Prepared fitted meshes for " + definitions.Length + " pig garments.";
    }

    public static void Prepare(ClothingDefinition clothing, bool force = false)
    {
        string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clothing.Model)).Substring(0,8);
        string path = Models + "/" + clothing.Model.name + " " + id + ".prefab";
        var savedModel = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (savedModel != null && !force) { Assign(clothing, savedModel); return; }
        foreach (string folder in new[] { Meshes, Models })
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, folder.Substring(folder.LastIndexOf('/') + 1));
        var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clothing.Model));
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        var pig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Pigs/PigAvatar.prefab");
        Transform pigModel = pig.GetComponent<PigAppearance>().ModelRoot;
        var body = pig.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == "Body");
        Matrix4x4 bodyToPig = pigModel.worldToLocalMatrix * body.transform.localToWorldMatrix;
        Vector3[] bodyPoints = body.sharedMesh.vertices.Select(bodyToPig.MultiplyPoint3x4).ToArray();
        Vector3[] bodyNormals = body.sharedMesh.normals.Select(n => bodyToPig.MultiplyVector(n).normalized).ToArray();
        BoneWeight[] bodyWeights = body.sharedMesh.boneWeights;
        int[] triangles = body.sharedMesh.triangles;
        var cells = new Dictionary<Vector3Int,List<int>>();
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3Int min = Cell(Vector3.Min(bodyPoints[triangles[t]], Vector3.Min(bodyPoints[triangles[t+1]],bodyPoints[triangles[t+2]])));
            Vector3Int max = Cell(Vector3.Max(bodyPoints[triangles[t]], Vector3.Max(bodyPoints[triangles[t+1]],bodyPoints[triangles[t+2]])));
            for(int x=min.x;x<=max.x;x++) for(int y=min.y;y<=max.y;y++) for(int z=min.z;z<=max.z;z++)
            {
                var cell = new Vector3Int(x,y,z);
                if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = new List<int>();
                list.Add(t);
            }
        }
        var model = UnityEngine.Object.Instantiate(clothing.Model);
        try
        {
            model.name = clothing.Model.name + " fitted";
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); model.transform.localScale = Vector3.one;
            int part = 0;
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = UnityEngine.Object.Instantiate(skin.sharedMesh);
                mesh.name = clothing.Model.name + " fitted skin";
                bool soft = clothing.Slot == ClothingSlot.Torso || clothing.Slot == ClothingSlot.Legs;
                if (soft)
                {
                    if(clothing.Slot == ClothingSlot.Torso) Subdivide(mesh);
                    Matrix4x4 toRoot = model.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                    var vertices = mesh.vertices; var weights = mesh.boneWeights;
                    var indices = skin.bones.Select((b,i) => (b.name,i)).ToDictionary(p => p.name,p => p.i);
                    int[] boneMap = body.bones.Select(b => indices[b.name]).ToArray();
                    float gap = clothing.Slot == ClothingSlot.Torso ? .07f : .035f;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 point = toRoot.MultiplyPoint3x4(vertices[i]);
                        int nearest = Nearest(point, bodyPoints, triangles, cells, out Vector3 closest, out Vector3 bary);
                        if (nearest < 0) continue;
                        Vector3 normal = (bodyNormals[triangles[nearest]] * bary.x + bodyNormals[triangles[nearest+1]] * bary.y + bodyNormals[triangles[nearest+2]] * bary.z).normalized;
                        float distance = Vector3.Dot(point - closest, normal);
                        if (distance < gap)
                            vertices[i] += toRoot.inverse.MultiplyVector(normal * Mathf.Min(.1f, gap - distance));
                        BoneWeight w = Blend(bodyWeights[triangles[nearest]],bodyWeights[triangles[nearest+1]],bodyWeights[triangles[nearest+2]],bary);
                        w.boneIndex0 = boneMap[w.boneIndex0]; w.boneIndex1 = boneMap[w.boneIndex1];
                        w.boneIndex2 = boneMap[w.boneIndex2]; w.boneIndex3 = boneMap[w.boneIndex3];
                        weights[i] = w;
                    }
                    if (clothing.Slot == ClothingSlot.Legs)
                    {
                        int hips = indices["Hips"];
                        for (int i = 0; i < weights.Length; i++)
                        {
                            BoneWeight w = weights[i];
                            int Keep(int index) => skin.bones[index].name.Contains("Arm") || skin.bones[index].name.Contains("Forearm") || skin.bones[index].name.Contains("Hand") ? hips : index;
                            w.boneIndex0 = Keep(w.boneIndex0); w.boneIndex1 = Keep(w.boneIndex1);
                            w.boneIndex2 = Keep(w.boneIndex2); w.boneIndex3 = Keep(w.boneIndex3); weights[i] = w;
                        }
                    }
                    mesh.vertices = vertices; mesh.boneWeights = weights;
                    mesh.RecalculateNormals(); mesh.RecalculateTangents();
                }
                for (int i = 0; i < mesh.subMeshCount && i < skin.sharedMaterials.Length; i++)
                    if (skin.sharedMaterials[i] != null && (skin.sharedMaterials[i].name.Contains("obvodka") ||
                        clothing.Slot == ClothingSlot.Legs && skin.sharedMaterials[i].name.EndsWith("_Light", StringComparison.Ordinal)))
                        mesh.SetTriangles(Array.Empty<int>(),i);
                mesh.RecalculateBounds();
                string meshPath = Meshes + "/" + clothing.Model.name + " " + id + " " + part++ + ".asset";
                mesh.name = System.IO.Path.GetFileNameWithoutExtension(meshPath); // asset main object name must match the file name
                var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (savedMesh == null) { AssetDatabase.CreateAsset(mesh,meshPath); savedMesh=mesh; }
                else { CopyMesh(mesh,savedMesh); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(savedMesh); }
                skin.sharedMesh = savedMesh; skin.updateWhenOffscreen = true;
            }
            savedModel = PrefabUtility.SaveAsPrefabAsset(model,path);
            Assign(clothing,savedModel);
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }

    private static void Assign(ClothingDefinition clothing, GameObject model)
    {
        if (clothing.WornModel == model) return;
        var data = new SerializedObject(clothing); data.FindProperty("pigWornModel").objectReferenceValue = model;
        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(clothing);
    }

    private static Vector3Int Cell(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x/CellSize),Mathf.FloorToInt(p.y/CellSize),Mathf.FloorToInt(p.z/CellSize));

    private static void CopyMesh(Mesh source, Mesh target)
    {
        target.Clear(false); target.indexFormat=source.indexFormat;target.name=source.name;
        target.vertices=source.vertices;target.normals=source.normals;target.uv=source.uv;target.tangents=source.tangents;
        target.boneWeights=source.boneWeights;target.bindposes=source.bindposes;
        target.subMeshCount=source.subMeshCount;
        for(int i=0;i<source.subMeshCount;i++)target.SetTriangles(source.GetTriangles(i),i);
        target.bounds=source.bounds; target.UploadMeshData(false);
    }

    // Give the joint bends enough vertices to follow the pig's rounded skin.
    private static void Subdivide(Mesh mesh)
    {
        var points=mesh.vertices.ToList(); var normals=mesh.normals.ToList(); var uv=mesh.uv.ToList();
        var weights=mesh.boneWeights.ToList(); var bindposes=mesh.bindposes;
        var edges=new Dictionary<ulong,int>();
        int Mid(int a,int b)
        {
            ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
            if(edges.TryGetValue(key,out int index))return index;
            index=points.Count; edges.Add(key,index);
            points.Add((points[a]+points[b])*.5f); normals.Add((normals[a]+normals[b]).normalized);
            uv.Add((uv[a]+uv[b])*.5f); weights.Add(Blend(weights[a],weights[b],weights[a],new Vector3(.5f,.5f,0)));
            return index;
        }
        var submeshes=new List<int[]>();
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            int[] source=mesh.GetTriangles(sub);var faces=new List<int>(source.Length*4);
            for(int i=0;i<source.Length;i+=3)
            {
                int a=source[i],b=source[i+1],c=source[i+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);
                faces.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});
            }
            submeshes.Add(faces.ToArray());
        }
        mesh.Clear();mesh.indexFormat=points.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(points);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.boneWeights=weights.ToArray();mesh.bindposes=bindposes;
        mesh.subMeshCount=submeshes.Count;for(int sub=0;sub<submeshes.Count;sub++)mesh.SetTriangles(submeshes[sub],sub);
    }

    private static int Nearest(Vector3 point, Vector3[] points, int[] triangles, Dictionary<Vector3Int,List<int>> cells, out Vector3 closest, out Vector3 bary)
    {
        Vector3Int center = Cell(point); int nearest = -1; float distance = float.PositiveInfinity;
        closest = bary = Vector3.zero;
        var seen = new HashSet<int>();
        for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++) for (int z = -2; z <= 2; z++)
            if (cells.TryGetValue(center + new Vector3Int(x,y,z),out var candidates))
                foreach (int index in candidates)
                {
                    if(!seen.Add(index)) continue;
                    Vector3 b = ClosestBarycentric(point,points[triangles[index]],points[triangles[index+1]],points[triangles[index+2]]);
                    Vector3 p = points[triangles[index]]*b.x+points[triangles[index+1]]*b.y+points[triangles[index+2]]*b.z;
                    float d = (p-point).sqrMagnitude;
                    if (d < distance) { distance=d; nearest=index; closest=p; bary=b; }
                }
        return nearest;
    }

    private static BoneWeight Blend(BoneWeight a, BoneWeight b, BoneWeight c, Vector3 bary)
    {
        var values = new float[16];
        Add(values,a,bary.x); Add(values,b,bary.y); Add(values,c,bary.z);
        var top = Enumerable.Range(0,16).OrderByDescending(i=>values[i]).Take(4).ToArray();
        float sum = top.Sum(i=>values[i]);
        return new BoneWeight {boneIndex0=top[0],boneIndex1=top[1],boneIndex2=top[2],boneIndex3=top[3],
            weight0=values[top[0]]/sum,weight1=values[top[1]]/sum,weight2=values[top[2]]/sum,weight3=values[top[3]]/sum};
    }
    private static void Add(float[] values, BoneWeight w, float factor)
    {
        values[w.boneIndex0]+=w.weight0*factor; values[w.boneIndex1]+=w.weight1*factor;
        values[w.boneIndex2]+=w.weight2*factor; values[w.boneIndex3]+=w.weight3*factor;
    }
    // Closest point on a triangle, expressed in barycentric coordinates.
    private static Vector3 ClosestBarycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab=b-a, ac=c-a, ap=p-a;
        float d1=Vector3.Dot(ab,ap), d2=Vector3.Dot(ac,ap);
        if(d1<=0 && d2<=0) return new Vector3(1,0,0);
        Vector3 bp=p-b; float d3=Vector3.Dot(ab,bp), d4=Vector3.Dot(ac,bp);
        if(d3>=0 && d4<=d3) return new Vector3(0,1,0);
        float vc=d1*d4-d3*d2;
        if(vc<=0 && d1>=0 && d3<=0) { float v=d1/(d1-d3); return new Vector3(1-v,v,0); }
        Vector3 cp=p-c; float d5=Vector3.Dot(ab,cp), d6=Vector3.Dot(ac,cp);
        if(d6>=0 && d5<=d6) return new Vector3(0,0,1);
        float vb=d5*d2-d1*d6;
        if(vb<=0 && d2>=0 && d6<=0) { float w=d2/(d2-d6); return new Vector3(1-w,0,w); }
        float va=d3*d6-d5*d4;
        if(va<=0 && d4-d3>=0 && d5-d6>=0) { float w=(d4-d3)/((d4-d3)+(d5-d6)); return new Vector3(0,1-w,w); }
        float denom=1/(va+vb+vc); float v2=vb*denom,w2=vc*denom;
        return new Vector3(1-v2-w2,v2,w2);
    }
}
