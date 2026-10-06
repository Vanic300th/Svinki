using System;
using System.IO;
using UnityEngine;

// Run through Unity Pipeline run_script; does not alter saved player preferences.
public static class PigCustomizationCapture
{
    public static string Gallery()
    {
        var menu = UnityEngine.Object.FindAnyObjectByType<PigAppearanceMenu>();
        var camera = menu.GetComponentInChildren<Camera>(true);
        var appearance = menu.GetComponentInChildren<PigAppearance>(true);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        Vector3 oldPosition = camera.transform.localPosition;
        Quaternion oldRotation = camera.transform.localRotation;
        int oldFace = appearance.FaceCode;
        var target = new RenderTexture(512, 640, 24); target.Create();
        var tile = new Texture2D(512, 640, TextureFormat.RGB24, false);
        var gallery = new Texture2D(1536, 1280, TextureFormat.RGB24, false);
        int[] colors = { 0, 1, 5, 3, 6, 4 };
        int[] patterns = { 0, 1, 2, 3, 4, 1 };
        try
        {
            camera.targetTexture = target;
            camera.transform.localPosition = new Vector3(1.05f, 1.06f, 3.7f);
            camera.transform.LookAt(camera.transform.parent.position + Vector3.up * 1.01f);
            for (int i = 0; i < 6; i++)
            {
                appearance.Apply(new PigFace { glassesStyle = i + 1, mohawk = true, nosePiercing = true,
                    skinColor = colors[i], pattern = patterns[i], hairColor = (i + 2) % 8,
                    patternColor = i == 1 ? 2 : 1, brows = i % 3, eyes = i % 2,
                    mustache = i == 0 ? 1 : 0, earPiercing = i % 2 == 0 }.Encode());
                camera.Render(); RenderTexture.active = target;
                tile.ReadPixels(new Rect(0, 0, 512, 640), 0, 0); tile.Apply();
                gallery.SetPixels(i % 3 * 512, (1 - i / 3) * 640, 512, 640, tile.GetPixels());
                File.WriteAllBytes("ArtSource/Pigs/v2/preview_" + PigAppearance.GlassesOptions[i + 1] + ".png", tile.EncodeToPNG());
            }
            gallery.Apply();
            File.WriteAllBytes("ArtSource/Pigs/v2/customization_gallery.png", gallery.EncodeToPNG());
            return "Captured six eyewear styles, palettes, mohawk, septum and markings.";
        }
        finally
        {
            appearance.Apply(oldFace); camera.targetTexture = oldTarget;
            camera.transform.localPosition = oldPosition; camera.transform.localRotation = oldRotation;
            RenderTexture.active = oldActive; target.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(tile); UnityEngine.Object.DestroyImmediate(gallery);
        }
    }

}
