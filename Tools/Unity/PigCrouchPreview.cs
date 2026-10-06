using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

// Run through Unity Pipeline in Play Mode; all preview objects are destroyed afterwards.
public static class PigCrouchPreview
{
    public static string Before() => Gallery("before");
    public static string After() => Gallery("squash");
    public static string Network() => Gallery("squash-network", "Assets/Prefabs/NetworkPlayer.prefab");

    static string Gallery(string name, string prefabPath = "Assets/Prefabs/OfflinePlayer.prefab")
    {
        NetworkLobby.Instance.StartCoroutine(Capture(name, prefabPath));
        return "Scheduled native rendered crouch capture: " + name;
    }

    static IEnumerator Capture(string name, string prefabPath)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var player = UnityEngine.Object.Instantiate(prefab, new Vector3(1000, 0, 0), Quaternion.identity);
        var studio = new GameObject("Temporary crouch preview");
        studio.transform.position = player.transform.position;
        foreach (var behaviour in player.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        var motor = player.GetComponent<GrayboxPlayerController>();
        var pig = player.GetComponentInChildren<PigAppearance>();
        var motion = pig.GetComponent<PigMotion>();
        var animator = player.transform.Find("Legacy Animation Driver").GetComponent<Animator>();
        var parts = pig.ModelRoot.GetComponentsInChildren<Transform>(true);
        foreach (var part in parts) part.gameObject.layer = 31;
        var skins = pig.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var cameraObject = new GameObject("Crouch preview camera", typeof(Camera));
        cameraObject.transform.SetParent(studio.transform, false);
        var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .2f, .26f);
        camera.orthographic = true; camera.orthographicSize = 1.35f;
        camera.nearClipPlane = .05f; camera.farClipPlane = 10;
        camera.transform.localPosition = new Vector3(2.5f, 1.35f, 4);
        camera.transform.LookAt(player.transform.position + new Vector3(0, 1.02f, .35f));
        foreach (Vector3 position in new[] { new Vector3(-2, 3, 3), new Vector3(2, 2, 2) })
        {
            var obj = new GameObject("Crouch preview light", typeof(Light));
            obj.transform.SetParent(studio.transform, false); obj.transform.localPosition = position;
            obj.transform.LookAt(player.transform.position + Vector3.up);
            var light = obj.GetComponent<Light>(); light.type = LightType.Spot;
            light.spotAngle = 95; light.range = 8; light.intensity = 8; light.cullingMask = 1 << 31;
        }
        var target = new RenderTexture(400, 640, 24); target.Create(); camera.targetTexture = target;
        var tile = new Texture2D(400, 640, TextureFormat.RGB24, false);
        var gallery = new Texture2D(2400, 640, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        var report = new List<object>();
        try
        {
            animator.speed = 0;
            animator.SetBool("Grounded", true); animator.SetLayerWeight(1, 0);
            var spring = typeof(PigMotion).GetMethod("UpdateSquash", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int pose = 0; pose < 6; pose++)
            {
                bool crouch = pose > 0 && pose < 5; float amount = !crouch ? 0 : pose == 1 ? .5f : 1; float speed = pose == 3 || pose == 4 ? 2.5f : 0;
                float time = pose == 2 ? .5f : pose == 3 ? .25f : pose == 4 ? .75f : 0;
                motor.SetRemoteStance(amount);
                typeof(PlayerAnimation).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player.GetComponent<PlayerAnimation>(), null);
                if (animator.GetFloat("Crouch") != 0) throw new Exception("Pig still uses the bending crouch clip.");
                animator.SetBool("Grounded", true); animator.SetFloat("Speed", speed);
                animator.Play("Locomotion", 0, time); animator.Update(0);
                for (int frame = 0; frame < (pose == 5 ? 9 : 90); frame++) spring.Invoke(motion, new object[] { amount, 1f / 60 });
                typeof(PigMotion).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motion, null);
                // Let Unity refresh the skin's matrices at the new nonuniform scale.
                yield return null;
                yield return null;
                camera.Render(); RenderTexture.active = target;
                float min = skins.Min(r => r.bounds.min.y), max = skins.Max(r => r.bounds.max.y);
                tile.ReadPixels(new Rect(0, 0, 400, 640), 0, 0); tile.Apply();
                gallery.SetPixels(pose * 400, 0, 400, 640, tile.GetPixels());
                var feet = parts.Where(t => t.name == "Foot_L" || t.name == "Foot_R").Select(t => t.position.y).ToArray();
                int links = ((System.Collections.ICollection)typeof(PigMotion).GetField("links", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motion)).Count;
                float lean = Vector3.Angle(parts.Single(t => t.name == "Spine").up, Vector3.up);
                float footError = Mathf.Abs(feet.Min() - .1f * pig.ModelRoot.localScale.y);
                if (name != "before" && (links != 15 || footError > .025f || lean > 12 || crouch && pig.ModelRoot.localScale.y >= 1 || pose == 5 && pig.ModelRoot.localScale.y <= 1.05f))
                    throw new Exception("Squash crouch lost rig bindings, upright posture, deformation or floor contact.");
                report.Add(new { pose, crouch, amount, lean, speed, time, min, max, feet, footError,
                    scale = new { x = pig.ModelRoot.localScale.x, y = pig.ModelRoot.localScale.y, z = pig.ModelRoot.localScale.z },
                    links,
                    legacy = motion.UsesLegacyClips });
            }
            for (int frame = 0; frame < 90; frame++) spring.Invoke(motion, new object[] { 0f, 1f / 60 });
            typeof(PigMotion).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motion, null);
            if ((pig.ModelRoot.localScale - Vector3.one).sqrMagnitude > .0001f)
                throw new Exception("Elastic crouch did not settle back to normal proportions.");
            gallery.Apply();
            File.WriteAllBytes("ArtSource/CrouchFix/" + name + ".png", gallery.EncodeToPNG());
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("ArtSource/CrouchFix/" + name + ".json", json);
            Debug.Log("Crouch capture passed: " + name);
        }
        finally
        {
            RenderTexture.active = oldActive; target.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(tile); UnityEngine.Object.DestroyImmediate(gallery);
            UnityEngine.Object.DestroyImmediate(studio); UnityEngine.Object.DestroyImmediate(player);
        }
    }
}
