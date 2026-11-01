using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PigAppearanceVerification
{
    [Serializable] private sealed class Report
    {
        public int combinations, presets, playerPrefabs;
        public int extendedCombinations, colors, patterns, modernRoundtrips;
        public bool menuButtons, offlineSpawn, networkLobbySelection, networkSpawn;
    }
    private static readonly Report Result = new Report();
    private static bool checkpointExisted;
    private static byte[] checkpointBytes;
    private static long originalFace;

    public static string ValidateAssets()
    {
        Result.combinations = Result.presets = Result.playerPrefabs = 0;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Pigs/PigAvatar.prefab");
        if (prefab == null) throw new Exception("Missing pig prefab.");
        GameObject root = UnityEngine.Object.Instantiate(prefab);
        try
        {
            PigAppearance appearance = root.GetComponent<PigAppearance>();
            var options = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("__")).ToArray();
            if (options.Length != 39) throw new Exception("Missing or duplicated appearance options: " + options.Length);
            SkinnedMeshRenderer body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Body");
            for (int eyes = 0; eyes < 2; eyes++) for (int brows = 0; brows < 3; brows++)
            for (int mustache = 0; mustache < 3; mustache++) for (int toggles = 0; toggles < 16; toggles++)
            {
                var face = new PigFace { eyes = eyes, brows = brows, mustache = mustache,
                    glasses = (toggles & 1) != 0, beard = (toggles & 2) != 0,
                    earPiercing = (toggles & 4) != 0, browPiercing = (toggles & 8) != 0 };
                long code = face.Encode();
                if (PigFace.Decode(code).Encode() != code) throw new Exception("Face roundtrip failed.");
                appearance.Apply(code);
                int expected = 2 + (face.mustache != 0 ? 1 : 0) + (face.glasses ? 1 : 0) + (face.beard ? 1 : 0) +
                    (face.earPiercing ? 1 : 0) + (face.browPiercing ? 1 : 0);
                if (options.Count(t => t.gameObject.activeSelf) != expected) throw new Exception("Incompatible face groups enabled.");
                if (!body.gameObject.activeInHierarchy) throw new Exception("Changing face hid the body.");
                foreach (var option in options.Where(t => t.gameObject.activeSelf))
                    if (!option.GetComponentsInChildren<SkinnedMeshRenderer>().All(r => r.bones.Any(b => b.name == "Head")))
                        throw new Exception("Accessory no longer shares Head bone.");
                Result.combinations++;
            }
            for (int i = 0; i < 3; i++)
            { appearance.Apply(PigFace.Preset(i).Encode()); if (appearance.FaceCode != PigFace.Preset(i).Encode()) throw new Exception("Preset failed."); Result.presets++; }
            ValidateExtended(appearance, body, options);
            ValidateModern(appearance,body,options);
            foreach (string path in new[] { SvinkiAssetPaths.NetworkPlayerPrefab, SvinkiAssetPaths.OfflinePlayerPrefab })
            {
                var player = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (player.GetComponentInChildren<PigAppearance>() == null || player.GetComponentInChildren<PigMotion>() == null)
                    throw new Exception("Player missing pig: " + path);
                if (player.GetComponent<FirstPersonBody>() == null) throw new Exception("Player missing first-person body.");
                if (player.transform.Find("Character Visual Human Backup").gameObject.activeSelf)
                    throw new Exception("Human visual still enabled.");
                Result.playerPrefabs++;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        return Save();
    }

    private static void ValidateExtended(PigAppearance appearance, SkinnedMeshRenderer body, Transform[] options)
    {
        Result.extendedCombinations = 0;
        var coordinates = new System.Collections.Generic.List<Vector3>();
        body.sharedMesh.GetUVs(3, coordinates);
        if (coordinates.Count != body.sharedMesh.vertexCount || coordinates.Max(p => p.y) < 1.6f)
            throw new Exception("Missing bind-pose marking coordinates.");
        if (body.sharedMaterial.shader.name != "Svinki/Pig Skin" || ShaderUtil.ShaderHasError(body.sharedMaterial.shader))
            throw new Exception("Pig marking shader failed.");
        for (int style = 0; style < PigFace.GlassesCount; style++)
        for (int toggles = 0; toggles < 4; toggles++)
        for (int color = 0; color < PigFace.SkinColorCount; color++)
        for (int pattern = 0; pattern < PigFace.PatternCount; pattern++)
        {
            var face = new PigFace { glassesStyle = style, mohawk = (toggles & 1) != 0,
                nosePiercing = (toggles & 2) != 0, skinColor = color, pattern = pattern,
                hairColor = (color + style) % PigFace.ColorCount, patternColor = (color + pattern) % PigFace.ColorCount,
                brows = 2, mustache = 2, beard = true, earPiercing = true, browPiercing = true };
            long code = face.Encode();
            if (PigFace.Sanitize(code) != code || PigFace.Decode(code).Encode() != code)
                throw new Exception("Extended appearance lost bits.");
            var request = new SessionRequest { Appearance = code };
            if (JsonUtility.FromJson<SessionRequest>(JsonUtility.ToJson(request)).Appearance != code)
                throw new Exception("Session appearance serialization lost bits.");
            appearance.Apply(code);
            int expected = 6 + (style != 0 ? 1 : 0) + (face.mohawk ? 1 : 0) + (face.nosePiercing ? 1 : 0);
            if (options.Count(t => t.gameObject.activeSelf) != expected)
                throw new Exception("Extended appearance group conflict.");
            if (options.Count(t => t.name.StartsWith("Glasses__") && t.gameObject.activeSelf) != (style == 0 ? 0 : 1))
                throw new Exception("Multiple glasses enabled.");
            foreach (var option in options.Where(t => t.gameObject.activeSelf))
                if (!option.GetComponentsInChildren<SkinnedMeshRenderer>().All(r => r.bones.Any(b => b.name == "Head")))
                    throw new Exception("New accessory lost Head bone.");
            var block = new MaterialPropertyBlock(); body.GetPropertyBlock(block, 0);
            if (block.GetColor("_BaseColor") != PigAppearance.SkinColors[color] || block.GetFloat("_Pattern") != pattern)
                throw new Exception("Skin palette or pattern failed.");
            if (block.GetColor("_PatternColor") != PigAppearance.HairColors[face.patternColor])
                throw new Exception("Marking palette failed.");
            Result.extendedCombinations++;
        }
        for (int code = 0; code < 512; code++)
        {
            var legacy = PigFace.Decode(code);
            if (legacy.skinColor != 0 || legacy.pattern != 0 || legacy.mohawk || legacy.nosePiercing || legacy.glassesStyle > 1)
                throw new Exception("Legacy face migration failed.");
        }
        foreach (int malformed in new[] { -1, int.MinValue, int.MaxValue, 511 << 17 })
            if (PigFace.Sanitize(PigFace.Sanitize(malformed)) != PigFace.Sanitize(malformed))
                throw new Exception("Appearance sanitization is not stable.");
        Result.colors = PigFace.SkinColorCount; Result.patterns = PigFace.PatternCount;
    }

    private static void ValidateModern(PigAppearance appearance,SkinnedMeshRenderer body,Transform[] options)
    {
        var random=new System.Random(3817);Result.modernRoundtrips=0;
        for(int i=0;i<1000;i++)
        {
            var face=new PigFace{eyes=random.Next(PigFace.EyesCount),brows=random.Next(PigFace.BrowsCount),mustache=random.Next(PigFace.MustacheCount),
                glassesStyle=random.Next(PigFace.GlassesCount),hairStyle=random.Next(PigFace.HairCount),skinColor=random.Next(PigFace.SkinColorCount),
                pattern=random.Next(PigFace.PatternCount),tattoo=random.Next(PigFace.TattooCount),tattooPlacement=random.Next(PigFace.PlacementCount),
                hairColor=random.Next(PigFace.ColorCount),hairAccentColor=random.Next(PigFace.ColorCount),hairTipColor=random.Next(PigFace.ColorCount),patternColor=random.Next(PigFace.ColorCount),
                beard=random.Next(2)==1,earPiercing=random.Next(2)==1,browPiercing=random.Next(2)==1,nosePiercing=random.Next(2)==1};
            long code=face.Encode();var decoded=PigFace.Decode(code);
            if(JsonUtility.ToJson(decoded)!=JsonUtility.ToJson(face)||PigFace.Sanitize(code)!=code)throw new Exception("Modern appearance fields lost bits.");
            var request=new SessionRequest{Appearance=code};
            if(JsonUtility.FromJson<SessionRequest>(JsonUtility.ToJson(request)).Appearance!=code)throw new Exception("64-bit session JSON lost appearance data.");
            var entry=new OutfitResultEntry{appearance=code};
            if(JsonUtility.FromJson<OutfitResultEntry>(JsonUtility.ToJson(entry)).appearance!=code)throw new Exception("Outfit archive lost appearance data.");
            appearance.Apply(code);
            int expected=2+(face.mustache>0?1:0)+(face.glassesStyle>0?1:0)+(face.beard?1:0)+(face.earPiercing?1:0)+(face.browPiercing?1:0)+(face.nosePiercing?1:0)+(face.hairStyle>0?1:0);
            if(options.Count(t=>t.gameObject.activeSelf)!=expected)throw new Exception("Modern option groups overlap.");
            var block=new MaterialPropertyBlock();body.GetPropertyBlock(block,0);
            if(block.GetFloat("_TattooPlacement")!=face.tattooPlacement)throw new Exception("Tattoo placement was not applied.");
            foreach(var option in options.Where(t=>t.gameObject.activeSelf))
                foreach(var renderer in option.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if(!renderer.enabled||!renderer.bones.Any(b=>b.name=="Head"))throw new Exception("An accessory is invisible or detached from the rig: "+option.name);
            if(face.hairStyle>0)
            {
                var hair=options.Single(t=>t.name=="Hair__"+PigAppearance.HairOptions[face.hairStyle]);
                if(PigAppearance.HairColorSlots[face.hairStyle]!=1)throw new Exception("Hair has more than one editable colour: "+hair.name);
                foreach(var renderer in hair.GetComponentsInChildren<SkinnedMeshRenderer>())
                    for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)
                        if(renderer.sharedMaterials[slot].name.StartsWith("Pig_Hair",StringComparison.Ordinal))
                        {
                            renderer.GetPropertyBlock(block,slot);
                            if(block.GetColor("_BaseColor")!=PigAppearance.HairColors[face.hairColor])
                                throw new Exception("Hair contains a second editable colour: "+hair.name);
                        }
            }
            Result.modernRoundtrips++;
        }
        foreach(long malformed in new[]{-1L,long.MinValue,long.MaxValue,1L<<55})
            if(PigFace.Sanitize(PigFace.Sanitize(malformed))!=PigFace.Sanitize(malformed))throw new Exception("64-bit sanitization is not stable.");
    }

    public static string VerifyMenu()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play mode.");
        originalFace = PigFace.Selected;
        var menu = UnityEngine.Object.FindAnyObjectByType<PigAppearanceMenu>();
        var buttons=menu.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        var preview=menu.GetComponentInChildren<PigAppearance>();
        void Click(string direction,string caption)=>buttons.Single(b=>b.name==direction+" "+caption).onClick.Invoke();
        try
        {
            menu.SelectFace(new PigFace().Encode());
            foreach(var test in new[]{("Eyes",PigFace.EyesCount),("Brows",PigFace.BrowsCount),("Hair style",PigFace.HairCount),("Glasses",PigFace.GlassesCount),("Moustache",PigFace.MustacheCount),("Skin colour",PigFace.SkinColorCount),("Pattern",PigFace.PatternCount),("Tattoo",PigFace.TattooCount)})
            {
                long start=preview.FaceCode;
                Click("Previous",test.Item1);Click("Next",test.Item1);
                if(preview.FaceCode!=start)throw new Exception("Backward/forward mismatch: "+test.Item1);
                for(int i=0;i<test.Item2;i++)Click("Next",test.Item1);
                if(preview.FaceCode!=start)throw new Exception("Full option cycle mismatch: "+test.Item1);
            }
            menu.SelectFace(new PigFace{hairStyle=8,tattoo=1}.Encode());
            foreach(string caption in new[]{"Hair colour","Tattoo placement"})Click("Next",caption);
            var face=PigFace.Decode(preview.FaceCode);
            if(face.hairColor!=1||face.tattooPlacement!=1||PigFace.Selected!=preview.FaceCode)
                throw new Exception("Hair colour or tattoo location failed to save.");
            if(buttons.Any(b=>b.name=="Next Hair accent"||b.name=="Next Hair tips"))
                throw new Exception("Obsolete multicolour controls remain in the menu.");
            foreach(string caption in new[]{"Hair colour"})
                if(!buttons.Single(b=>b.name=="Next "+caption).gameObject.activeInHierarchy)throw new Exception("Missing hair colour submenu: "+caption);
            menu.SelectFace(new PigFace().Encode());
            if(buttons.Single(b=>b.name=="Next Hair colour").gameObject.activeInHierarchy||buttons.Single(b=>b.name=="Next Tattoo placement").gameObject.activeInHierarchy)
                throw new Exception("Conditional controls did not close.");
            var scroll=menu.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Single();
            var ear=scroll.content.Find("Ear piercing");var brow=scroll.content.Find("Brow piercing");var nose=scroll.content.Find("Nose ring");
            if(brow.GetSiblingIndex()!=ear.GetSiblingIndex()+1||nose.GetSiblingIndex()!=ear.GetSiblingIndex()+2)throw new Exception("Piercings are not adjacent.");
            Result.menuButtons=true;return Save();
        }
        finally {menu.SelectFace(originalFace);}
    }

    public static string VerifyOffline()
    {
        var lobby = NetworkLobby.Instance;
        if (!lobby.Offline || lobby.Busy) throw new Exception("Offline game is still loading.");
        var pig = UnityEngine.Object.FindObjectsByType<PigAppearance>()
            .Single(p => p.GetComponentInParent<GrayboxPlayerController>() != null);
        if (pig.FaceCode != PigFace.Selected) throw new Exception("Offline pig lost the selected face.");
        if (pig.GetComponent<PigMotion>() == null) throw new Exception("Pig motion missing.");
        var fps = pig.GetComponentInParent<FirstPersonBody>();
        if (fps.transform.Find("First person body") == null) throw new Exception("First-person torso missing.");
        if (fps.GetComponentsInChildren<SkinnedMeshRenderer>().Count(r => r.name.StartsWith("First person Hoof_")) != 4)
            throw new Exception("First-person hooves missing.");
        Result.offlineSpawn = true;
        return Save();
    }

    public static void BackupCheckpoint()
    {
        checkpointExisted = File.Exists(CheckpointStore.DefaultPath);
        checkpointBytes = checkpointExisted ? File.ReadAllBytes(CheckpointStore.DefaultPath) : null;
    }
    public static void RestoreCheckpoint()
    {
        if (checkpointExisted) File.WriteAllBytes(CheckpointStore.DefaultPath, checkpointBytes);
        else if (File.Exists(CheckpointStore.DefaultPath)) File.Delete(CheckpointStore.DefaultPath);
        PigFace.SaveSelection(originalFace);
    }
    public static string VerifyLobbySelection()
    {
        var lobby = NetworkLobby.Instance;
        var self = lobby.Snapshot.players.Single(p => p.id == lobby.Identity);
        if (self.appearance != PigFace.Selected || self.ready) throw new Exception("Lobby face selection not published/reset to unready.");
        Result.networkLobbySelection = true;
        return Save();
    }
    public static string VerifyNetworkSpawn()
    {
        var lobby = NetworkLobby.Instance;
        var player = UnityEngine.Object.FindObjectsByType<NetworkPlayer>()
            .Single(p => p.IsOwner && p.ParticipantId == lobby.Identity);
        if (player.GetComponentInChildren<PigAppearance>().FaceCode != PigFace.Selected)
            throw new Exception("Network spawn lost selected face.");
        if (!player.IsServerStarted || !player.IsClientStarted) throw new Exception("Host client/server not started.");
        Result.networkSpawn = true;
        return Save();
    }
    private static string Save()
    {
        string json = JsonUtility.ToJson(Result, true);
        File.WriteAllText("ArtSource/Pigs/v3/unity_validation_report.json", json);
        return json;
    }
}
