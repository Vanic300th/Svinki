using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class LobbyPolishCheck
{
    private static void Need(bool value, string message) { if (!value) throw new Exception(message); }
    private static T[] Items<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    public static string Depth()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit mode required");
        var scene = SceneManager.GetSceneByName("SampleScene");
        var root = new GameObject("World text occlusion check"); SceneManager.MoveGameObjectToScene(root, scene);
        var cameraGo = new GameObject("Camera"); cameraGo.transform.SetParent(root.transform); cameraGo.layer = 31;
        var camera = cameraGo.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 2;
        camera.transform.position = new Vector3(0,0,-4); camera.backgroundColor = Color.black; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.cullingMask = 1 << 31; camera.nearClipPlane = .1f; camera.farClipPlane = 10;
        cameraGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform); wall.layer = 31;
        wall.transform.position = new Vector3(0,0,-1); wall.transform.localScale = new Vector3(8,8,.2f);
        var wallRenderer = wall.GetComponent<Renderer>(); var wallMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        wallMaterial.color = new Color(0,.3f,0); wallRenderer.sharedMaterial = wallMaterial;
        var target = RenderTexture.GetTemporary(512,512,24); var prior = RenderTexture.active; var image = new Texture2D(512,512,TextureFormat.RGB24,false);
        int Capture()
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0,0,512,512),0,0); image.Apply();
            return image.GetPixels32().Count(c => c.r > 150 && c.b > 150 && c.g < 100);
        }
        var report = "";
        try
        {
            foreach (bool useTmp in new[] { false, true })
            {
                var labelGo = new GameObject("TEST"); labelGo.transform.SetParent(root.transform); labelGo.layer = 31;
                Material material;
                if (useTmp)
                {
                    var label = labelGo.AddComponent<TextMeshPro>(); label.font = Items<TextMeshPro>(scene).First().font;
                    label.text = "TEST"; label.fontSize = 12; label.alignment = TextAlignmentOptions.Center; label.color = Color.magenta;
                    label.rectTransform.sizeDelta = new Vector2(8,3);
                    material = new Material(label.font.material) { shader = Shader.Find("Friendslop/World TMP Distance Field") };
                    label.fontSharedMaterial = material; label.ForceMeshUpdate();
                }
                else
                {
                    var label = labelGo.AddComponent<TextMesh>(); label.font = Items<TextMesh>(scene).First().font;
                    label.fontSize = 64; label.characterSize = .8f; label.anchor = TextAnchor.MiddleCenter; label.color = Color.magenta;
                    label.font.RequestCharactersInTexture("TEST",64,FontStyle.Normal); label.text = "TEST";
                    material = new Material(label.font.material) { shader = Shader.Find("Friendslop/World Legacy Text") };
                    label.GetComponent<Renderer>().sharedMaterial = material;
                }
                wallRenderer.enabled = false; int visible = Capture(); Need(visible > 50, "Text not rendered: " + useTmp + " pixels=" + visible);
                wallRenderer.enabled = true; int hidden = Capture(); Need(hidden == 0, "Text visible through wall: " + useTmp + " pixels=" + hidden);
                Directory.CreateDirectory("ArtSource/LobbyPolish"); File.WriteAllBytes("ArtSource/LobbyPolish/" + (useTmp ? "tmp" : "legacy") + "-occluded.png", image.EncodeToPNG());
                report += (useTmp ? "TMP" : "Legacy") + ": " + visible + " visible pixels, " + hidden + " through wall.\n";
                UnityEngine.Object.DestroyImmediate(labelGo); UnityEngine.Object.DestroyImmediate(material);
            }
            foreach (var name in new[] { "FriendslopLobby", "SampleScene" })
            {
                var s = SceneManager.GetSceneByName(name);
                foreach (var label in Items<WorldTextDepth>(s))
                {
                    var settings = new SerializedObject(label); Need(settings.FindProperty("depthShader").objectReferenceValue != null, "Missing depth shader");
                    Need(label.GetComponent<Renderer>().sharedMaterial.shader.name.StartsWith("Friendslop/World"), "Missing world material");
                }
            }
            File.WriteAllText("ArtSource/LobbyPolish/depth-validation.txt", report); return report;
        }
        finally
        {
            RenderTexture.active = prior; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(wallMaterial);
        }
    }
    public static async Task<string> Music()
    {
        Need(EditorApplication.isPlaying, "Play mode required");
        Need(NetworkLobby.Instance == null, "Run this fixture in the isolated lobby scene, outside a live session");
        var scene = SceneManager.GetSceneByName("FriendslopLobby"); var manager = Items<LobbyActivityManager>(scene).Single();
        var background = Items<AudioSource>(scene).Single(a => a.name == "Ambient Music");
        var tv = Items<LobbyTelevision>(scene).Single(); var juke = Items<LobbyJukebox>(scene).Single();
        var field = typeof(LobbyMediaPlayer).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        // Inject a received state without a transport or any media download in this local audio fixture.
        var value = typeof(SyncVar<LobbyMediaState>).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic);
        void State(LobbyMediaPlayer player, bool playing, bool loaded = true) => value.SetValue(field.GetValue(player), new LobbyMediaState { Playing = playing, Url = loaded ? "https://example.invalid/check.mp4" : "", Version = 100 });
        async Task Wait() { await Task.Delay(1800); }
        var mediaPlayers = new LobbyMediaPlayer[] { tv, juke };
        var oldStates = mediaPlayers.Select(m => m.State).ToArray();
        var active = mediaPlayers.Select(m => m.gameObject.activeSelf).ToArray();
        var networked = mediaPlayers.Select(m => m.GetComponent<FishNet.Object.NetworkObject>().GetIsNetworked()).ToArray();
        bool enabled = manager.enabled; var report = "";
        try
        {
            manager.enabled = false;
            foreach (var media in new LobbyMediaPlayer[] { tv, juke })
            {
                media.GetComponent<FishNet.Object.NetworkObject>().SetIsNetworked(false);
                media.gameObject.SetActive(true);
            }
            State(tv, false, false); State(juke, false, false); await Wait();
            Need(background.isPlaying && Math.Abs(background.volume - .22f) < .001f, "Idle ambient missing"); report += "PASS idle ambient\n";
            State(tv, true); await Wait(); Need(!background.isPlaying && background.volume == 0, "TV did not suppress ambient"); report += "PASS TV fades and pauses ambient\n";
            State(juke, true); State(tv, false); await Wait(); Need(!background.isPlaying && background.volume == 0, "Jukebox did not retain suppression"); report += "PASS jukebox keeps ambient suppressed after TV pauses\n";
            State(juke, false); await Wait(); Need(background.isPlaying && Math.Abs(background.volume - .22f) < .001f, "Paused media did not restore ambient"); report += "PASS media pause restores ambient\n";
            State(juke, true); await Wait(); Need(!background.isPlaying && background.volume == 0, "Jukebox did not suppress ambient");
            State(juke, false, false); await Wait(); Need(background.isPlaying && Math.Abs(background.volume - .22f) < .001f, "Stopped media did not restore ambient"); report += "PASS stop restores ambient\n";
            foreach (var depth in Items<WorldTextDepth>(scene)) Need(depth.GetComponent<Renderer>().sharedMaterial.shader.name.StartsWith("Friendslop/World"), "Runtime world material lost");
            // Exercise localization's font replacement, which previously restored the GUI overlay shader.
            for (int i = 0; i < 100 && !GameLanguage.Ready; i++) await Task.Delay(100);
            Need(GameLanguage.Ready, "Localization did not initialize");
            var labelGo = new GameObject("Localized depth check"); labelGo.SetActive(false);
            var label = labelGo.AddComponent<TextMesh>(); label.font = GameLanguage.Font.sourceFontFile; label.text = "STAFF ONLY";
            var world = labelGo.AddComponent<WorldTextDepth>(); var serialized = new SerializedObject(world);
            serialized.FindProperty("depthShader").objectReferenceValue = Shader.Find("Friendslop/World Legacy Text"); serialized.ApplyModifiedPropertiesWithoutUndo();
            labelGo.AddComponent<GameWorldLocalizedText>().Source = "STAFF ONLY";
            var previousLocale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
            try
            {
                labelGo.SetActive(true); await Task.Delay(500);
                foreach (string code in new[] { "ru", "en" })
                {
                    UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(code);
                    await Task.Delay(1500); Need(GameLanguage.Ready, "Locale switch did not finish");
                    Need(label.GetComponent<Renderer>().sharedMaterial.shader.name == "Friendslop/World Legacy Text", "Legacy depth lost on language switch");
                    Need(label.GetComponent<Renderer>().sharedMaterial.mainTexture == label.font.material.mainTexture, "Wrong localized font atlas");
                    foreach (var depth in Items<WorldTextDepth>(scene).Where(d => d.GetComponent<TextMeshPro>() != null))
                        Need(depth.GetComponent<Renderer>().sharedMaterial.shader.name == "Friendslop/World TMP Distance Field", "TMP depth lost on language switch");
                }
                report += "PASS RU/EN localized font atlas changes preserve world depth\n";
            }
            finally
            {
                UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = previousLocale;
                UnityEngine.Object.Destroy(labelGo);
            }
            Directory.CreateDirectory("ArtSource/LobbyPolish"); File.WriteAllText("ArtSource/LobbyPolish/music-validation.txt",report); return report;
        }
        finally
        {
            for (int i = 0; i < mediaPlayers.Length; i++)
            {
                value.SetValue(field.GetValue(mediaPlayers[i]), oldStates[i]);
                mediaPlayers[i].gameObject.SetActive(active[i]);
                mediaPlayers[i].GetComponent<FishNet.Object.NetworkObject>().SetIsNetworked(networked[i]);
            }
            manager.enabled = enabled;
        }
    }
}
