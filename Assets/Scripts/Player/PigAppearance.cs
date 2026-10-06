using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct PigFace
{
    public int eyes, brows, mustache;
    public bool beard, earPiercing, browPiercing, mohawk, nosePiercing;
    public int glassesStyle, skinColor, pattern, hairColor, patternColor, tattoo;
    public const int GlassesCount = 7, ColorCount = 8, PatternCount = 5, TattooCount = 5;
    // Keep the original nine bits intact so saved v1 faces and presets still work.
    public bool glasses { get => glassesStyle != 0; set => glassesStyle = value ? Mathf.Max(1, glassesStyle) : 0; }

    public int Encode() => Mathf.Clamp(eyes, 0, 1) | Mathf.Clamp(brows, 0, 2) << 1 |
        Mathf.Clamp(mustache, 0, 2) << 3 | (glasses ? 32 : 0) | (beard ? 64 : 0) |
        (earPiercing ? 128 : 0) | (browPiercing ? 256 : 0) |
        Mathf.Clamp(glassesStyle - 1, 0, GlassesCount - 2) << 9 |
        (mohawk ? 1 << 12 : 0) | (nosePiercing ? 1 << 13 : 0) |
        Mathf.Clamp(skinColor, 0, ColorCount - 1) << 14 |
        Mathf.Clamp(pattern, 0, PatternCount - 1) << 17 |
        Mathf.Clamp(hairColor, 0, ColorCount - 1) << 20 |
        Mathf.Clamp(patternColor, 0, ColorCount - 1) << 23 | Mathf.Clamp(tattoo, 0, TattooCount - 1) << 26;

    public static PigFace Decode(int value) => new PigFace
    {
        eyes = value & 1, brows = Mathf.Min((value >> 1) & 3, 2),
        mustache = Mathf.Min((value >> 3) & 3, 2),
        glassesStyle = (value & 32) == 0 ? 0 : Mathf.Min(((value >> 9) & 7) + 1, GlassesCount - 1),
        beard = (value & 64) != 0, earPiercing = (value & 128) != 0,
        browPiercing = (value & 256) != 0, mohawk = (value & (1 << 12)) != 0,
        nosePiercing = (value & (1 << 13)) != 0, skinColor = (value >> 14) & 7,
        pattern = Mathf.Min((value >> 17) & 7, PatternCount - 1),
        hairColor = (value >> 20) & 7, patternColor = (value >> 23) & 7, tattoo = Mathf.Min((value >> 26) & 7, TattooCount - 1)
    };

    public static int Sanitize(int value) => Decode(Mathf.Max(0, value) & ((1 << 29) - 1)).Encode();
    public static PigFace Preset(int index) => index == 0
        ? new PigFace { brows = 1, glasses = true, mustache = 1 }
        : index == 1 ? new PigFace { beard = true, mustache = 2 }
        : new PigFace { eyes = 1, brows = 2, earPiercing = true, browPiercing = true };

    private const string PreferenceKey = "svinki.pig.face.v1";
    public static int Selected => Sanitize(PlayerPrefs.GetInt(PreferenceKey, Preset(0).Encode()));
    public static void SaveSelection(int value)
    {
        PlayerPrefs.SetInt(PreferenceKey, Sanitize(value));
        PlayerPrefs.Save();
    }
}

/// <summary>Independent accessories on a shared pig rig; no mesh is rebuilt when a face changes.</summary>
[DisallowMultipleComponent]
public sealed class PigAppearance : MonoBehaviour
{
    [SerializeField] private Transform modelRoot;
    [SerializeField] private int faceCode = 42;
    private readonly Dictionary<string, GameObject> options = new Dictionary<string, GameObject>();
    private readonly List<Renderer> coloredParts = new List<Renderer>();
    private MaterialPropertyBlock colors;
    public static readonly string[] GlassesOptions = { "", "Round", "Square", "CatEye", "Aviator", "SunRound", "SunSquare" };
    public static readonly string[] ColorNames = { "pink", "cream", "chocolate", "grey", "mint", "blue", "lavender", "coral" };
    public static readonly string[] HairColorNames = { "brown", "black", "blond", "white", "mint", "blue", "purple", "magenta" };
    public static readonly string[] PatternNames = { "solid", "spots", "stripes", "saddle", "socks" };
    public static readonly string[] TattooNames = { "none", "heart", "star", "lightning", "arm stripes" };
    public static readonly Color[] SkinColors = {
        new Color(.81f, .365f, .32f), new Color(.94f, .80f, .63f), new Color(.32f, .16f, .10f), new Color(.48f, .51f, .56f),
        new Color(.38f, .77f, .60f), new Color(.36f, .62f, .89f), new Color(.68f, .46f, .82f), new Color(.95f, .39f, .28f) };
    public static readonly Color[] HairColors = {
        new Color(.085f, .037f, .024f), new Color(.018f, .021f, .028f), new Color(.88f, .61f, .20f), new Color(.90f, .90f, .92f),
        new Color(.12f, .80f, .46f), new Color(.08f, .30f, .88f), new Color(.52f, .12f, .78f), new Color(.92f, .08f, .38f) };
    public int FaceCode => faceCode;
    public Transform ModelRoot => modelRoot != null ? modelRoot : transform;

    private void Awake() => Apply(faceCode);
    public void Bind(Transform model) { modelRoot = model; options.Clear(); coloredParts.Clear(); Apply(faceCode); }

    public void Apply(int value)
    {
        faceCode = PigFace.Sanitize(value);
        if (options.Count == 0)
        {
            foreach (Transform part in ModelRoot.GetComponentsInChildren<Transform>(true))
                if (part.name.Contains("__")) options[part.name] = part.gameObject;
            coloredParts.AddRange(ModelRoot.GetComponentsInChildren<Renderer>(true));
        }
        PigFace face = PigFace.Decode(faceCode);
        string eyes = face.eyes == 0 ? "Eyes__Open" : "Eyes__Squint";
        string brows = face.brows == 0 ? "Brows__Neutral" : face.brows == 1 ? "Brows__Raised" : "Brows__Confident";
        foreach (var option in options)
        {
            string name = option.Key;
            bool visible = name == eyes || name == brows || face.glasses && name == "Glasses__" + GlassesOptions[face.glassesStyle] ||
                name == "Mustache__Curled" && face.mustache == 1 || name == "Mustache__Classic" && face.mustache == 2 ||
                name == "Beard__Short" && face.beard || name == "EarPiercing__Hoop" && face.earPiercing ||
                name == "BrowPiercing__Bar" && face.browPiercing || name == "Hair__Mohawk" && face.mohawk ||
                name == "NosePiercing__Septum" && face.nosePiercing;
            option.Value.SetActive(visible);
        }
        ApplyColors(face);
    }

    private void ApplyColors(PigFace face)
    {
        if (colors == null) colors = new MaterialPropertyBlock();
        foreach (Renderer part in coloredParts)
        {
            Material[] materials = part.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;
                string name = material.name;
                bool skin = name.StartsWith("Pig_Skin", StringComparison.Ordinal);
                bool ear = name.StartsWith("Pig_InnerEar", StringComparison.Ordinal);
                bool snout = name.StartsWith("Pig_Snout", StringComparison.Ordinal);
                bool hair = name.StartsWith("Pig_FacialHair", StringComparison.Ordinal);
                if (!skin && !ear && !snout && !hair) continue;
                part.GetPropertyBlock(colors, i);
                Color color = hair ? HairColors[face.hairColor] : SkinColors[face.skinColor];
                // Preserve the darker snout and inner ears in the original pink palette.
                if (ear) color = face.skinColor == 0 ? new Color(.63f, .22f, .20f) : color * .77f;
                if (snout) color = face.skinColor == 0 ? new Color(.73f, .235f, .23f) : color * .88f;
                color.a = 1;
                colors.SetColor("_BaseColor", color);
                colors.SetColor("_Color", color);
                colors.SetColor("_PatternColor", HairColors[face.patternColor]);
                colors.SetFloat("_Pattern", skin ? face.pattern : 0);
                colors.SetFloat("_Tattoo", skin ? face.tattoo : 0);
                part.SetPropertyBlock(colors, i);
            }
        }
    }
}
