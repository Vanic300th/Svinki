using System;
using System.IO;
using System.Collections.Generic;
using Unity.AI.Navigation;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LobbyPolishSetup
{
    private static T[] Items<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static Scene Load(string name)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit mode required");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/" + name + ".unity");
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
        if (scene.isDirty) throw new Exception("Save pending edits in " + name);
        return scene;
    }
    private static readonly FieldInfo Id = typeof(NetworkObject).GetField("SceneId", BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Need(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static string ApplyMusic()
    {
        var lobby = Load("FriendslopLobby"); var factory = Load("SampleScene");
        var source = Items<AudioSource>(factory).Single(a => a.name == "Ambient Music");
        var existing = Items<AudioSource>(lobby).SingleOrDefault(a => a.name == "Ambient Music");
        if (existing == null)
        {
            var root = new GameObject("Ambient Music"); SceneManager.MoveGameObjectToScene(root, lobby);
            existing = root.AddComponent<AudioSource>(); EditorUtility.CopySerialized(source, existing);
            existing.volume = .22f; existing.loop = true; existing.playOnAwake = true; existing.spatialBlend = 0;
        }
        var music = existing.GetComponent<RoundBackgroundMusic>() ?? existing.gameObject.AddComponent<RoundBackgroundMusic>();
        var media = Items<LobbyMediaPlayer>(lobby);
        Need(media.Length == 2, "Expected TV and jukebox");
        var settings = new SerializedObject(music); var list = settings.FindProperty("suppressWhilePlaying");
        list.arraySize = media.Length;
        for (int i = 0; i < media.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = media[i];
        settings.FindProperty("fadeSeconds").floatValue = 1; settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(lobby); Need(EditorSceneManager.SaveScene(lobby), "Music save failed");
        return "PASS: quiet lobby ambient, existing clip/mixer; TV and jukebox suppression wired.";
    }
    public static string ApplySigns()
    {
        var legacyShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/WorldLegacyText.shader");
        var tmpShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/WorldTMPText.shader");
        Need(legacyShader != null && tmpShader != null && !ShaderUtil.ShaderHasError(legacyShader) && !ShaderUtil.ShaderHasError(tmpShader), "World text shader import failed");
        Directory.CreateDirectory("Assets/Materials/WorldText"); AssetDatabase.Refresh();
        int count = 0;
        foreach (var name in new[] { "FriendslopLobby", "SampleScene" })
        {
            var scene = Load(name);
            foreach (var label in Items<TextMesh>(scene).Cast<Component>().Concat(Items<TextMeshPro>(scene)))
            {
                var tmp = label as TextMeshPro; var old = label as TextMesh;
                var original = tmp != null ? tmp.font.material : old.font.material;
                var shader = tmp != null ? tmpShader : legacyShader;
                string fontPath = AssetDatabase.GetAssetPath(tmp != null ? (UnityEngine.Object)tmp.font : old.font);
                string path = "Assets/Materials/WorldText/" + (tmp != null ? "TMP-" : "Legacy-") + AssetDatabase.AssetPathToGUID(fontPath) + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(original) { shader = shader }; AssetDatabase.CreateAsset(material, path); }
                var depth = label.GetComponent<WorldTextDepth>() ?? label.gameObject.AddComponent<WorldTextDepth>();
                var settings = new SerializedObject(depth); settings.FindProperty("depthShader").objectReferenceValue = shader; settings.ApplyModifiedPropertiesWithoutUndo();
                if (tmp != null) { tmp.isOverlay = false; tmp.fontSharedMaterial = material; }
                else label.GetComponent<Renderer>().sharedMaterial = material;
                EditorUtility.SetDirty(label); count++;
            }
            EditorSceneManager.MarkSceneDirty(scene); Need(EditorSceneManager.SaveScene(scene), "Sign save failed");
        }
        AssetDatabase.SaveAssets(); return "PASS: " + count + " world labels use depth-tested shaders; font changes preserve occlusion.";
    }
    public static string DoubleMannequins()
    {
        var scene = Load("SampleScene");
        var parent = scene.GetRootGameObjects().Single(g => g.name == "Graybox Level").transform;
        if (parent.Find("Doubled mannequin population") != null) { CheckPopulation(scene); return "Already doubled; population verified."; }
        Need(Items<ThrowableMannequin>(scene).Length == 40 && Items<MannequinBrain>(scene).Length == 8 && Items<ThiefBrain>(scene).Length == 5 && Items<MannequinHeadWatcher>(scene).Length == 4, "Unexpected starting population");
        var originals = Items<NetworkObject>(scene).ToDictionary(n => n, n => (ulong)Id.GetValue(n));
        var actors = Items<ThrowableMannequin>(scene).Cast<Component>().Concat(Items<MannequinBrain>(scene)).Concat(Items<ThiefBrain>(scene)).Concat(Items<MannequinHeadWatcher>(scene)).Select(c => c.gameObject).Distinct().ToArray();
        Need(actors.Length == 57, "Overlapping actor types");
        var group = new GameObject("Doubled mannequin population").transform; group.SetParent(parent, false);
        var watchers = new List<Transform>();
        foreach (var original in actors)
        {
            var copy = UnityEngine.Object.Instantiate(original, group); copy.name = original.name + " additional";
            foreach (var network in copy.GetComponentsInChildren<NetworkObject>(true)) network.SetSceneId(0);
            if (copy.GetComponent<MannequinHeadWatcher>() != null) watchers.Add(copy.transform);
        }
        var create = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic);
        var serialize = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var network in (List<NetworkObject>)create.Invoke(null, new object[] { scene, false, 0 })) serialize.Invoke(network, new object[] { true, false });
        Need(originals.All(p => (ulong)Id.GetValue(p.Key) == p.Value), "Existing network IDs changed");
        foreach (var surface in Items<NavMeshSurface>(scene)) surface.AddData();
        var helper = new GameObject("Population placement"); SceneManager.MoveGameObjectToScene(helper, scene);
        try
        {
            Physics.SyncTransforms(); var layout = helper.AddComponent<RoundWorldLayout>(); layout.Shuffle(4017);
            foreach (var watcher in watchers)
            {
                Need(layout.TryTakePoint(watcher.name, .65f, out var point), "No safe watcher position");
                watcher.position = point; watcher.rotation = Quaternion.Euler(0, 180, 0);
            }
            Physics.SyncTransforms();
        }
        finally { UnityEngine.Object.DestroyImmediate(helper); }
        CheckPopulation(scene);
        EditorSceneManager.MarkSceneDirty(scene); Need(EditorSceneManager.SaveScene(scene), "Population save failed");
        return "PASS: 57 -> 114 mannequins (80 neutral / 16 stalkers / 10 thieves / 8 watchers); all " + originals.Count + " original network IDs preserved.";
    }
    private static void CheckPopulation(Scene scene)
    {
        Need(Items<ThrowableMannequin>(scene).Length == 80 && Items<MannequinBrain>(scene).Length == 16 && Items<ThiefBrain>(scene).Length == 10 && Items<MannequinHeadWatcher>(scene).Length == 8, "Population was not exactly doubled");
        var ids = Items<NetworkObject>(scene).Select(n => (ulong)Id.GetValue(n)).ToArray();
        Need(ids.All(id => id != 0) && ids.Distinct().Count() == ids.Length, "Invalid network IDs");
    }
    public static string Inspect()
    {
        string result = "";
        foreach (var name in new[] { "FriendslopLobby", "SampleScene" })
        {
            var scene = Load(name);
            result += name + "\n";
            foreach (var text in Items<TextMeshPro>(scene))
                result += "TMP " + text.name + " | " + text.text.Replace('\n', ' ') + " | " + text.fontSharedMaterial.shader.name + " | ZTest=" + (text.fontSharedMaterial.HasProperty("_ZTestMode") ? text.fontSharedMaterial.GetFloat("_ZTestMode").ToString() : "none") + "\n";
            foreach (var text in Items<TextMesh>(scene)) result += "Legacy " + text.name + " | " + text.text + " | " + text.GetComponent<Renderer>().sharedMaterial.shader.name + "\n";
            result += "Neutral=" + Items<ThrowableMannequin>(scene).Length + " Brain=" + Items<MannequinBrain>(scene).Length + " Thief=" + Items<ThiefBrain>(scene).Length + " Watcher=" + Items<MannequinHeadWatcher>(scene).Length + "\n";
            foreach (var audio in Items<AudioSource>(scene).Where(a => a.clip != null)) result += "Audio " + audio.name + " | " + audio.clip.name + " | " + audio.volume + " | " + (audio.outputAudioMixerGroup != null ? audio.outputAudioMixerGroup.name : "none") + "\n";
        }
        return result;
    }
}
