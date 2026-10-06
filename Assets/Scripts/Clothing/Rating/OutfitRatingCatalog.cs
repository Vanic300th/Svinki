using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ClothingPattern { Plain, Woven, Denim, Stripes, Camouflage }

[Serializable]
public sealed class ClothingRatingProfile
{
    public ClothingDefinition clothing;
    public string key;
    public Color color = Color.gray;
    public ClothingPattern pattern;
    [Range(0, 1)] public float dressiness;
    [Range(0, 1)] public float warmth;
}

[Serializable]
public sealed class ReferenceOutfit
{
    public string name;
    public ClothingDefinition[] items = Array.Empty<ClothingDefinition>();
}

[Serializable]
public sealed class OutfitRating
{
    public int score, matching, palette, patterns, completeness, ratedItems;
    public string verdict, strength, improvement;
}

/// <summary>Deterministic compatibility rules calibrated to the six supplied outfits.</summary>
[CreateAssetMenu(menuName = "Svinki/Оценка образов")]
public sealed class OutfitRatingCatalog : ScriptableObject
{
    public ClothingRatingProfile[] items = Array.Empty<ClothingRatingProfile>();
    public ReferenceOutfit[] references = Array.Empty<ReferenceOutfit>();
    public static OutfitRatingCatalog Load() => Resources.Load<OutfitRatingCatalog>("OutfitRating");

    public ClothingRatingProfile Profile(ClothingDefinition clothing) => clothing == null ? null :
        items.FirstOrDefault(p => p != null && p.clothing == clothing);
    public ClothingDefinition FindClothing(string assetName) => items.FirstOrDefault(p => p != null && p.clothing != null && p.clothing.name == assetName)?.clothing;

    public OutfitRating Evaluate(IEnumerable<ClothingDefinition> outfit)
    {
        // Count a slot once. A caller cannot inflate the score with duplicate items.
        var selected = (outfit ?? Enumerable.Empty<ClothingDefinition>()).Where(c => c != null)
            .OrderBy(c => c.name, StringComparer.Ordinal).GroupBy(c => c.Slot).Select(g => g.First()).ToArray();
        var known = selected.Select(Profile).Where(p => p != null).OrderBy(p => p.clothing.Slot).ToArray();
        var result = new OutfitRating { ratedItems = known.Length, completeness = selected.Length * 25 };
        if (known.Length == 0)
        {
            result.verdict = selected.Length == 0 ? "No outfit yet" : "Not rated";
            result.strength = "Collect clothes that look good together.";
            result.improvement = selected.Length == 0 ? "No clothing found yet." : "These clothes do not have rating rules yet.";
            return result;
        }
        float matching = 0, colors = 0, patterns = 0, pairWeights = 0, patternWeights = 0;
        float best = -1, worst = 101;
        ClothingRatingProfile bestA = null, bestB = null, worstA = null, worstB = null;
        for (int a = 0; a < known.Length; a++) for (int b = a + 1; b < known.Length; b++)
        {
            var first = known[a]; var second = known[b];
            float weight = PairWeight(first.clothing.Slot, second.clothing.Slot);
            float color = ColorMatch(first.color, second.color);
            float match = PairMatch(first, second, color);
            matching += match * weight; colors += color * weight; pairWeights += weight;
            if (first.clothing.Slot != ClothingSlot.Feet && second.clothing.Slot != ClothingSlot.Feet)
            { patterns += PatternMatch(first, second) * weight; patternWeights += weight; }
            if (match > best) { best = match; bestA = first; bestB = second; }
            if (match < worst) { worst = match; worstA = first; worstB = second; }
        }
        // A single known garment has no incompatible partner, but cannot exceed 25/100.
        result.matching = pairWeights > 0 ? Mathf.RoundToInt(matching / pairWeights) : 100;
        result.palette = pairWeights > 0 ? Mathf.RoundToInt(colors / pairWeights) : 100;
        result.patterns = patternWeights > 0 ? Mathf.RoundToInt(patterns / patternWeights) : 100;
        float quality = result.matching * .65f + result.palette * .20f + result.patterns * .15f;
        result.score = Mathf.Clamp(Mathf.RoundToInt(quality * known.Length / 4f), 0, 100);
        result.verdict = result.score >= 90 ? "Complete look" : result.score >= 75 ? "Great combination" :
            result.score >= 60 ? "Promising look" : result.completeness < 100 ? "Outfit in progress" : "Mixed look";
        result.strength = bestA != null ? bestA.clothing.DisplayName + " and " + bestB.clothing.DisplayName +
            (best >= 90 ? " go really well together." : " are the best pair in your outfit.") : "First item found — start building your outfit.";
        if (selected.Length > known.Length) result.improvement = "Some clothes are not rated yet: rules cover " + known.Length + " of " + selected.Length + ".";
        else if (selected.Length < 4) result.improvement = "Missing: " + string.Join(", ", Enumerable.Range(0, 4).Select(i => (ClothingSlot)i)
            .Where(slot => !selected.Any(c => c.Slot == slot)).Select(SlotName)) + ".";
        else if (worst < 78) result.improvement = "Try a different pair: " + worstA.clothing.DisplayName + " + " + worstB.clothing.DisplayName + ".";
        else if (result.patterns < 75) result.improvement = "Several bold patterns clash. Try adding one solid-colour item.";
        else result.improvement = "All four pieces work together. Try experimenting with the details.";
        return result;
    }

    public static string SlotName(ClothingSlot slot) => slot == ClothingSlot.Head ? "headwear" :
        slot == ClothingSlot.Torso ? "top" : slot == ClothingSlot.Legs ? "trousers" : "footwear";

    private bool ProvenPair(ClothingRatingProfile a, ClothingRatingProfile b) => references.Any(r => r != null && r.items != null &&
        r.items.Contains(a.clothing) && r.items.Contains(b.clothing));
    private float PairMatch(ClothingRatingProfile a, ClothingRatingProfile b, float color)
    {
        if (ProvenPair(a, b)) return 100;
        float formality = 100 * (1 - Mathf.Abs(a.dressiness - b.dressiness));
        float season = 100 * (1 - Mathf.Abs(a.warmth - b.warmth));
        float style = formality * .8f + season * .2f;
        return Mathf.Clamp(20 + style * .55f + color * .25f, 25, 94);
    }
    private float PatternMatch(ClothingRatingProfile a, ClothingRatingProfile b)
    {
        if (ProvenPair(a, b)) return 100;
        bool activeA = a.pattern == ClothingPattern.Stripes || a.pattern == ClothingPattern.Camouflage;
        bool activeB = b.pattern == ClothingPattern.Stripes || b.pattern == ClothingPattern.Camouflage;
        if (!activeA || !activeB) return 92;
        return a.pattern == b.pattern ? 78 : 35;
    }
    private static float ColorMatch(Color a, Color b)
    {
        Color.RGBToHSV(a, out float h1, out float s1, out float v1);
        Color.RGBToHSV(b, out float h2, out float s2, out float v2);
        if (s1 < .18f || s2 < .18f || v1 < .22f || v2 < .22f) return 96;
        float difference = Mathf.Min(Mathf.Abs(h1 - h2), 1 - Mathf.Abs(h1 - h2));
        if (difference < .15f) return 94;
        if (difference >= .38f) return 90;
        return 72;
    }
    private static float PairWeight(ClothingSlot a, ClothingSlot b)
    {
        if (a > b) { var swap = a; a = b; b = swap; }
        if (a == ClothingSlot.Torso && b == ClothingSlot.Legs) return .30f;
        if (a == ClothingSlot.Torso && b == ClothingSlot.Feet) return .22f;
        if (a == ClothingSlot.Legs && b == ClothingSlot.Feet) return .16f;
        if (b == ClothingSlot.Torso) return .14f;
        if (b == ClothingSlot.Legs) return .10f;
        return .08f;
    }
}
