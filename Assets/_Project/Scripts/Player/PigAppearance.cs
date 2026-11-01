using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct PigFace
{
    public int eyes, brows, mustache, glassesStyle, skinColor, pattern, hairColor, patternColor, tattoo;
    public bool beard, earPiercing, browPiercing, nosePiercing;
    public int hairStyle, hairAccentColor, hairTipColor, tattooPlacement;
    public const int EyesCount=3, BrowsCount=6, MustacheCount=5, GlassesCount=13, HairCount=11;
    public const int ColorCount=12, SkinColorCount=12, PatternCount=6, TattooCount=5, PlacementCount=4;
    public bool glasses { get=>glassesStyle!=0; set=>glassesStyle=value?Mathf.Max(1,glassesStyle):0; }
    public bool mohawk { get=>hairStyle==1; set=>hairStyle=value?1:0; }
    private static long Bits(int value,int maximum,int shift)=>((long)Mathf.Clamp(value,0,maximum))<<shift;
    // Low 30 bits retain v1/v2 fields. Extra choices occupy named, non-overlapping high bits.
    public long Encode()=>Bits(eyes&1,1,0)|Bits(brows&3,3,1)|Bits(mustache&3,3,3)|
        (glasses?32L:0)|(beard?64L:0)|(earPiercing?128L:0)|(browPiercing?256L:0)|
        Bits(Mathf.Max(0,glassesStyle-1)&7,7,9)|(mohawk?1L<<12:0)|(nosePiercing?1L<<13:0)|
        Bits(skinColor&7,7,14)|Bits(pattern,PatternCount-1,17)|Bits(hairColor&7,7,20)|
        Bits(patternColor&7,7,23)|Bits(tattoo,TattooCount-1,26)|Bits(skinColor>>3,1,29)|
        Bits(Mathf.Max(0,glassesStyle-1)>>3,1,30)|Bits(eyes>>1,1,31)|Bits(brows>>2,1,32)|
        Bits(mustache>>2,1,33)|Bits(hairStyle>1?hairStyle:0,HairCount-1,34)|
        Bits(hairAccentColor&7,7,38)|Bits(hairTipColor&7,7,41)|Bits(tattooPlacement,PlacementCount-1,44)|Bits(hairColor>>3,1,46)|Bits(patternColor>>3,1,47)|Bits(hairAccentColor>>3,1,48)|Bits(hairTipColor>>3,1,49);
    public static PigFace Decode(long value)
    {
        int Read(int shift,int mask)=>(int)(value>>shift)&mask;
        int hair=Read(34,15);
        return new PigFace {
            eyes=Mathf.Min(Read(0,1)|(Read(31,1)<<1),EyesCount-1), brows=Mathf.Min(Read(1,3)|(Read(32,1)<<2),BrowsCount-1),
            mustache=Mathf.Min(Read(3,3)|(Read(33,1)<<2),MustacheCount-1),
            glassesStyle=(value&32)==0?0:Mathf.Min((Read(9,7)|(Read(30,1)<<3))+1,GlassesCount-1),
            beard=(value&64)!=0,earPiercing=(value&128)!=0,browPiercing=(value&256)!=0,nosePiercing=(value&(1L<<13))!=0,
            hairStyle=hair==0?((value&(1L<<12))!=0?1:0):Mathf.Min(hair,HairCount-1),
            skinColor=Mathf.Min(Read(14,7)|(Read(29,1)<<3),SkinColorCount-1),pattern=Mathf.Min(Read(17,7),PatternCount-1),
            hairColor=Mathf.Min(Read(20,7)|(Read(46,1)<<3),ColorCount-1),patternColor=Mathf.Min(Read(23,7)|(Read(47,1)<<3),ColorCount-1),tattoo=Mathf.Min(Read(26,7),TattooCount-1),
            hairAccentColor=Mathf.Min(Read(38,7)|(Read(48,1)<<3),ColorCount-1),hairTipColor=Mathf.Min(Read(41,7)|(Read(49,1)<<3),ColorCount-1),tattooPlacement=Read(44,3) };
    }
    public static long Sanitize(long value)=>Decode(Math.Max(0,value)&((1L<<50)-1)).Encode();
    public static PigFace Preset(int index)=>index==0?new PigFace{brows=1,glasses=true,mustache=1}:
        index==1?new PigFace{beard=true,mustache=2}:new PigFace{eyes=1,brows=2,earPiercing=true,browPiercing=true};
    public static long Selected=>long.TryParse(PlayerPrefs.GetString("svinki.pig.face.v3",""),out long value)?
        Sanitize(value):Sanitize(PlayerPrefs.GetInt("svinki.pig.face.v1",(int)Preset(0).Encode()));
    public static void SaveSelection(long value)
    {
        PlayerPrefs.SetString("svinki.pig.face.v3",Sanitize(value).ToString(System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }
}

/// <summary>Independent accessories on a shared pig rig; no mesh is rebuilt when a face changes.</summary>
[DisallowMultipleComponent]
public sealed class PigAppearance : MonoBehaviour
{
    [SerializeField] private Transform modelRoot;
    [SerializeField] private long faceCode = 42;
    private readonly Dictionary<string, GameObject> options = new Dictionary<string, GameObject>();
    private readonly List<Renderer> coloredParts = new List<Renderer>();
    private MaterialPropertyBlock colors;
    public static readonly string[] GlassesOptions = { "", "Round", "Square", "CatEye", "Aviator", "SunRound", "SunSquare", "Heart", "GiantRound", "Pixel", "Kamina", "MiddleFinger", "Spiked" };
    public static readonly string[] EyesNames={"open","squinting","bulging"};
    public static readonly string[] BrowsNames={"relaxed","raised","cheeky","unibrow","bushy","angry"};
    public static readonly string[] HairNames={"none","mohawk","curtains","ponytail","barcode","bowl cut","emo fringe","bob","dreadlocks","pompadour","leopard buzz"};
    public static readonly string[] HairOptions={"","Mohawk","Curtains","Ponytail","Barcode","Bowl","Emo","SplitBob","Dreads","Pompadour","LeopardBuzz"};
    public static readonly int[] HairColorSlots={0,1,1,1,1,1,1,1,1,1,1};
    public static readonly string[] GlassesNames={"none","round","square","cat-eye","aviator","round sunglasses","square sunglasses","hearts","giant round","pixel shades","Kamina","middle fingers","spiked frame"};
    public static readonly string[] MustacheNames={"none","curled","straight","very bushy","long curled"};
    public static readonly string[] PlacementNames={"arm","shoulder blade","leg","back"};
    public static readonly string[] ColorNames = { "pink", "cream", "chocolate", "grey", "mint", "blue", "lavender", "coral", "ivory", "sand", "cinnamon", "charcoal" };
    public static readonly string[] HairColorNames = { "brown", "black", "blond", "white", "mint", "blue", "purple", "magenta", "copper", "red", "teal", "navy" };
    public static readonly string[] PatternNames = { "solid", "spots", "stripes", "leopard", "checkered", "stars" };
    public static readonly string[] TattooNames = { "none", "heart", "star", "lightning", "bands" };
    public static readonly Color[] SkinColors = {
        Hex("ECA4B6"), Hex("ECDCAB"), Hex("8D604B"), Hex("8A909A"), Hex("9BC7AD"), Hex("9BBADB"), Hex("B9A1D2"), Hex("E6A28C"), Hex("F3ECDB"), Hex("CBB28A"), Hex("AA6C49"), Hex("4C4A4B") };
    public static readonly Color[] HairColors = {
        Hex("503C32"), Hex("383333"), Hex("D8BE8D"), Hex("EDE8DF"), Hex("ADC9B6"), Hex("A5BCD5"), Hex("B9A3CB"), Hex("D6A5BA"), Hex("D8894B"), Hex("D95E72"), Hex("64AA9D"), Hex("354F86") };
    private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
    public long FaceCode => faceCode;
    public Transform ModelRoot => modelRoot != null ? modelRoot : transform;

    private void Awake() => Apply(faceCode);
    public void Bind(Transform model) { modelRoot = model; options.Clear(); coloredParts.Clear(); Apply(faceCode); }

    public void Apply(long value)
    {
        long next = PigFace.Sanitize(value);
        if (options.Count > 0 && next == faceCode) return;
        faceCode = next;
        if (options.Count == 0)
        {
            foreach (Transform part in ModelRoot.GetComponentsInChildren<Transform>(true))
                if (part.name.Contains("__")) options[part.name] = part.gameObject;
            coloredParts.AddRange(ModelRoot.GetComponentsInChildren<Renderer>(true));
        }
        PigFace face = PigFace.Decode(faceCode);
        string eyes = "Eyes__" + new[]{"Open","Squint","Bulging"}[face.eyes];
        string brows = "Brows__" + new[]{"Neutral","Raised","Confident","Unibrow","Thick","Angry"}[face.brows];
        foreach (var option in options)
        {
            string name = option.Key;
            bool visible = name == eyes || name == brows || face.glasses && name == "Glasses__" + GlassesOptions[face.glassesStyle] ||
                name == "Mustache__Curled" && face.mustache == 1 || name == "Mustache__Classic" && face.mustache == 2 ||
                name == "Mustache__Bushy" && face.mustache == 3 || name == "Mustache__LongCurled" && face.mustache == 4 ||
                name == "Beard__Goatee" && face.beard || name == "EarPiercing__Hoop" && face.earPiercing ||
                name == "BrowPiercing__Bar" && face.browPiercing || face.hairStyle > 0 && name == "Hair__" + HairOptions[face.hairStyle] ||
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
                bool hair = name.StartsWith("Pig_FacialHair", StringComparison.Ordinal) || name.StartsWith("Pig_Hair",StringComparison.Ordinal);
                if (!skin && !ear && !snout && !hair) continue;
                part.GetPropertyBlock(colors, i);
                Color color = hair ? HairColors[face.hairColor] : SkinColors[face.skinColor];
                // Muted rosy accents stay related to every skin tone, including natural dark coats.
                if (ear) color = Color.Lerp(color, Hex("D99A9E"), .28f);
                if (snout) color = Color.Lerp(color, Hex("DCA4A8"), .20f);
                color.a = 1;
                colors.SetColor("_BaseColor", color);
                colors.SetColor("_Color", color);
                colors.SetColor("_PatternColor", HairColors[face.patternColor]);
                colors.SetFloat("_Pattern", skin ? face.pattern : 0);
                colors.SetFloat("_Tattoo", skin ? face.tattoo : 0);
                colors.SetFloat("_TattooPlacement",face.tattooPlacement);
                part.SetPropertyBlock(colors, i);
            }
        }
    }
}
