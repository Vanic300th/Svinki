using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Run in the user's existing Play session; no player inventory or checkpoint is changed.
public static class PigClothingCheck
{
    private static bool raw;
    public static string Raw() { raw = true; return Start(); }
    public static string Start()
    {
        if (!Application.isPlaying || NetworkLobby.Instance == null) throw new InvalidOperationException("A running lobby is required.");
        SessionState.SetString("Svinki.Clothing3D.Check", "running");
        NetworkLobby.Instance.StartCoroutine(Capture());
        return "Scheduled six outfits, each standing, raising arms and squashed.";
    }

    private static void Require(bool value, string message)
    {
        if (value) return;
        SessionState.SetString("Svinki.Clothing3D.Check", "FAILED: " + message);
        throw new InvalidOperationException(message);
    }

    private static IEnumerator Capture()
    {
        var all = AssetDatabase.FindAssets("", new[] { "Assets/ClothingLibrary/Generated/Definitions" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ClothingDefinition>)
            .Where(c => c != null && c.PigRigged).ToArray();
        Require(all.Length == 15, "Missing wardrobe definitions.");
        Require(all.All(c => c.WornModel != c.Model), "A fitted mesh is missing.");
        var heads = all.Where(c => c.Slot == ClothingSlot.Head).OrderBy(c => c.Model.name).ToArray();
        var tops = all.Where(c => c.Slot == ClothingSlot.Torso).OrderBy(c => c.Model.name).ToArray();
        var pants = all.Where(c => c.Slot == ClothingSlot.Legs).OrderBy(c => c.Model.name).ToArray();
        var shoes = all.Where(c => c.Slot == ClothingSlot.Feet).OrderBy(c => c.Model.name).ToArray();
        var actor = new GameObject("Temporary Pig Clothing Check"); actor.transform.position = new Vector3(1000, 0, 0);
        var pigObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Pigs/PigAvatar.prefab"), actor.transform);
        var pig = pigObject.GetComponent<PigAppearance>(); pig.Apply(PigFace.Preset(0).Encode());
        pigObject.GetComponent<PigMotion>().enabled = false;
        var originalBones = pig.ModelRoot.GetComponentsInChildren<Transform>(true);
        var poses = originalBones.ToDictionary(b => b, b => new Pose(b.localPosition, b.localRotation));
        var bodySkin=pigObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="Body");
        var nakedMesh=bodySkin.sharedMesh;
        var boneSet = new HashSet<Transform>(originalBones);
        var outfit = actor.AddComponent<PlayerOutfit>();
        actor.AddComponent<PlayerAvatar>().enabled = false;
        var renderer = actor.AddComponent<WorldOutfitRenderer>(); renderer.enabled = false;
        var data = new SerializedObject(renderer); data.FindProperty("characterVisual").objectReferenceValue = pigObject.transform; data.ApplyModifiedPropertiesWithoutUndo();
        typeof(WorldOutfitRenderer).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(renderer, null);
        var studio = new GameObject("Temporary Clothing Studio"); studio.transform.position = actor.transform.position;
        var cameraObject = new GameObject("Clothing check camera", typeof(Camera)); cameraObject.transform.SetParent(studio.transform, false);
        var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << 31;
        camera.orthographic = true; camera.orthographicSize = 1.16f; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f,.16f,.2f); camera.nearClipPlane = .05f; camera.farClipPlane = 12;
        camera.transform.localPosition = new Vector3(2.6f,1.55f,4.6f); camera.transform.LookAt(actor.transform.position + new Vector3(0,1,.12f));
        foreach (Vector3 position in new[] { new Vector3(-2,3,3), new Vector3(2,2,2) })
        {
            var lamp = new GameObject("Clothing check light", typeof(Light)); lamp.transform.SetParent(studio.transform, false); lamp.transform.localPosition = position;
            lamp.transform.LookAt(actor.transform.position + Vector3.up); var light = lamp.GetComponent<Light>();
            light.type = LightType.Spot; light.spotAngle = 105; light.range = 9; light.intensity = 9; light.cullingMask = 1 << 31;
        }
        var target = new RenderTexture(320,520,24); target.Create(); camera.targetTexture = target;
        var tile = new Texture2D(320,520,TextureFormat.RGB24,false);
        var gallery = new Texture2D(1920,1560,TextureFormat.RGB24,false);
        var active = RenderTexture.active;
        var report = new List<object>();
        var copies = new List<ClothingDefinition>();
        try
        {
            var pickups = AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ClothingLibrary/Generated/Pickups"})
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p=>p.GetComponent<ClothingPickup>()?.Clothing?.PigRigged==true).ToArray();
            foreach (GameObject prefab in pickups)
            {
                outfit.SetItems(Array.Empty<ClothingDefinition>());
                var item = UnityEngine.Object.Instantiate(prefab,actor.transform.position + Vector3.forward*5,Quaternion.identity);
                try
                {
                    item.GetComponent<NetworkPickup>().enabled=false;
                    item.GetComponent<FishNet.Object.NetworkObject>().enabled=false;
                    var clothing = item.GetComponent<ClothingPickup>(); var pickup=item.GetComponent<PickupItem>();
                    Require(!pickup.IsTargeted,"Pickup is highlighted without targeting.");
                    pickup.SetTargeted(true); Require(pickup.IsTargeted,"Target highlighting is unavailable.");
                    pickup.SetTargeted(false);
                    Require(item.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials)
                        .All(m=>!m.HasProperty("_EmissionColor") || m.GetColor("_EmissionColor").maxColorComponent<.001f),"Pickup glows while untargeted.");
                    Require(clothing.CanCollect(actor),"Clothing cannot be collected.");
                    pickup.PickUp(actor);
                    Require(outfit.Get(clothing.Clothing.Slot)==clothing.Clothing && !pickup.IsAvailable,"Pickup did not equip or hide correctly.");
                }
                finally { UnityEngine.Object.DestroyImmediate(item); }
            }
            outfit.SetItems(Array.Empty<ClothingDefinition>());
            yield return null;
            for (int look = 0; look < 6; look++)
            {
                var piecesForLook = new[] { heads[look%3], tops[look], pants[look%3], shoes[(look+1)%3] };
                if(raw) piecesForLook = piecesForLook.Select(c=> { var copy=UnityEngine.Object.Instantiate(c); var so=new SerializedObject(copy); so.FindProperty("pigWornModel").objectReferenceValue=null; so.ApplyModifiedPropertiesWithoutUndo(); copies.Add(copy); return copy; }).ToArray();
                outfit.SetItems(piecesForLook);
                yield return null;
                for (int pose = 0; pose < 3; pose++)
                {
                    foreach (var pair in poses) pair.Key.SetLocalPositionAndRotation(pair.Value.position, pair.Value.rotation);
                    pig.ModelRoot.localScale = pose == 2 ? new Vector3(1.4f,.58f,1.28f) : Vector3.one;
                    if (pose == 1)
                    {
                        foreach (Transform bone in originalBones.Where(b => b.name.StartsWith("UpperArm_")))
                            bone.rotation = Quaternion.AngleAxis(-65, pig.ModelRoot.right) * bone.rotation;
                        foreach (Transform bone in originalBones.Where(b => b.name.StartsWith("Thigh_")))
                            bone.rotation = Quaternion.AngleAxis(bone.name.EndsWith("L") ? 22 : -22, pig.ModelRoot.right) * bone.rotation;
                    }
                    foreach (Transform t in actor.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                    var slots = (IDictionary)typeof(WorldOutfitRenderer).GetField("equipped",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
                    Require(slots.Count == 4, "A slot failed to equip.");
                    var skins = new List<SkinnedMeshRenderer>();
                    foreach (DictionaryEntry entry in slots)
                    {
                        var holder = (GameObject)entry.Value;
                        Require(holder.transform.parent == pig.ModelRoot, "Clothing did not inherit the pig deformation.");
                        var pieces = holder.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        Require(pieces.Length == 1 && pieces[0].bones.All(boneSet.Contains), "Garment still uses its source skeleton.");
                        Require(pieces[0].sharedMesh.triangles.Length>0,"Garment has no rendered faces.");
                        var baked=new Mesh(); pieces[0].BakeMesh(baked);
                        Require(baked.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z))&&baked.bounds.size.magnitude<4,"Skinning produced an invalid garment.");
                        UnityEngine.Object.DestroyImmediate(baked);
                        Require(holder.GetComponentInChildren<WornGarmentFollower>() == null, "Rigged clothing is fitted as a rigid billboard.");
                        skins.AddRange(pieces);
                    }
                    yield return null; yield return null;
                    camera.Render(); RenderTexture.active = target; tile.ReadPixels(new Rect(0,0,320,520),0,0); tile.Apply();
                    gallery.SetPixels(look*320,(2-pose)*520,320,520,tile.GetPixels());
                    report.Add(new { look, pose, clothes = outfit.Items.Select(c => c.DisplayName).ToArray(),
                        boundBones = skins.Select(s => s.bones.Length).ToArray(),
                        bounds = skins.Select(s => new { s.name, center=s.bounds.center.ToString(),size=s.bounds.size.ToString() }).ToArray() });
                }
            }
            gallery.Apply(); Directory.CreateDirectory("ArtSource/Clothing3D");
            outfit.SetItems(Array.Empty<ClothingDefinition>());
            Require(bodySkin.sharedMesh==nakedMesh,"Removing clothes did not restore the full body.");
            File.WriteAllBytes("ArtSource/Clothing3D/"+(raw?"original-fit.png":"outfits.png"),gallery.EncodeToPNG());
            File.WriteAllText("ArtSource/Clothing3D/fitting.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            SessionState.SetString("Svinki.Clothing3D.Check", "passed: 15 pickups, 18 poses, all 16 bones rebound per garment; glow only on hover");
        }
        finally
        {
            RenderTexture.active = active; target.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(tile); UnityEngine.Object.DestroyImmediate(gallery);
            UnityEngine.Object.DestroyImmediate(studio); UnityEngine.Object.DestroyImmediate(actor);
            foreach(var c in copies) UnityEngine.Object.DestroyImmediate(c);
        }
    }
}
