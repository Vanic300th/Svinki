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
        public int extendedCombinations, colors, patterns;
        public bool menuButtons, offlineSpawn, networkLobbySelection, networkSpawn;
    }
    private static readonly Report Result = new Report();
    private static bool checkpointExisted;
    private static byte[] checkpointBytes;
    private static int originalFace;

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
            if (options.Length != 18) throw new Exception("Missing or duplicated appearance options: " + options.Length);
            SkinnedMeshRenderer body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Body");
            for (int eyes = 0; eyes < 2; eyes++) for (int brows = 0; brows < 3; brows++)
            for (int mustache = 0; mustache < 3; mustache++) for (int toggles = 0; toggles < 16; toggles++)
            {
                var face = new PigFace { eyes = eyes, brows = brows, mustache = mustache,
                    glasses = (toggles & 1) != 0, beard = (toggles & 2) != 0,
                    earPiercing = (toggles & 4) != 0, browPiercing = (toggles & 8) != 0 };
                int code = face.Encode();
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
            foreach (string path in new[] { "Assets/Prefabs/NetworkPlayer.prefab", "Assets/Prefabs/OfflinePlayer.prefab" })
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
        for (int color = 0; color < PigFace.ColorCount; color++)
        for (int pattern = 0; pattern < PigFace.PatternCount; pattern++)
        {
            var face = new PigFace { glassesStyle = style, mohawk = (toggles & 1) != 0,
                nosePiercing = (toggles & 2) != 0, skinColor = color, pattern = pattern,
                hairColor = (color + style) % PigFace.ColorCount, patternColor = (color + pattern) % PigFace.ColorCount,
                brows = 2, mustache = 2, beard = true, earPiercing = true, browPiercing = true };
            int code = face.Encode();
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
        Result.colors = PigFace.ColorCount; Result.patterns = PigFace.PatternCount;
    }

    public static string VerifyMenu()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play mode.");
        originalFace = PigFace.Selected;
        var menu = UnityEngine.Object.FindAnyObjectByType<PigAppearanceMenu>();
        var buttons = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);
        var preview = menu.GetComponentInChildren<PigAppearance>();
        buttons.Single(b => b.name == "Борода").onClick.Invoke();
        if (preview.FaceCode != PigFace.Preset(1).Encode()) throw new Exception("Beard preset button failed.");
        buttons.Single(b => b.name.StartsWith("Очки:")).onClick.Invoke();
        buttons.Single(b => b.name.StartsWith("Серьга в ухе:")).onClick.Invoke();
        buttons.Single(b => b.name.StartsWith("Пирсинг брови:")).onClick.Invoke();
        if (preview.FaceCode != 496 || PigFace.Selected != 496) throw new Exception("Independent accessories or saved selection failed.");
        buttons.Single(b => b.name == "Пирсинг").onClick.Invoke();
        if (preview.FaceCode != PigFace.Preset(2).Encode()) throw new Exception("Piercing preset failed.");
        foreach (string prefix in new[] { "Ирокез:", "Кольцо в носу:", "Цвет кожи:", "Окрас:", "Цвет узора:", "Цвет волос:" })
            buttons.Single(b => b.name.StartsWith(prefix)).onClick.Invoke();
        var extended = PigFace.Decode(PigFace.Selected);
        if (!extended.mohawk || !extended.nosePiercing || extended.skinColor != 1 || extended.pattern != 1 ||
            extended.patternColor != 1 || extended.hairColor != 1 || preview.FaceCode != PigFace.Selected)
            throw new Exception("Extended menu controls failed.");
        for (int style = 1; style < PigFace.GlassesCount; style++)
        {
            buttons.Single(b => b.name.StartsWith("Очки:")).onClick.Invoke();
            if (PigFace.Decode(preview.FaceCode).glassesStyle != style) throw new Exception("Glasses menu cycle failed.");
        }
        var scroll = menu.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Single();
        Canvas.ForceUpdateCanvases();
        if (scroll.content.rect.height <= scroll.viewport.rect.height || scroll.verticalScrollbar == null)
            throw new Exception("Appearance settings do not scroll.");
        menu.SelectFace(originalFace);
        Result.menuButtons = true;
        return Save();
    }

    public static string VerifyOffline()
    {
        var lobby = NetworkLobby.Instance;
        if (!lobby.Offline || lobby.Busy) throw new Exception("Offline game is still loading.");
        var pig = UnityEngine.Object.FindObjectsByType<PigAppearance>(FindObjectsSortMode.None)
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
        var player = UnityEngine.Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None)
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
        File.WriteAllText("ArtSource/Pigs/v2/unity_validation_report.json", json);
        return json;
    }
}
