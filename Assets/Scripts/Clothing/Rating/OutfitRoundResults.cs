using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class OutfitRoundResults
{
    public int round;
    public OutfitResultEntry[] entries = Array.Empty<OutfitResultEntry>();

    public static OutfitResultEntry Rate(OutfitRatingCatalog rules, string id, string nickname, int appearance,
        IEnumerable<ClothingDefinition> outfit)
    {
        var clothes = (outfit ?? Enumerable.Empty<ClothingDefinition>()).Where(c => c != null).OrderBy(c => c.Slot).ToArray();
        return new OutfitResultEntry { id = id, nickname = nickname, appearance = appearance,
            clothing = clothes.Select(c => c.name).ToArray(), names = clothes.Select(c => c.DisplayName).ToArray(), rating = rules.Evaluate(clothes) };
    }
}

[Serializable]
public sealed class OutfitResultEntry
{
    public string id, nickname;
    public int appearance;
    public int presentationPose;
    public string[] clothing = Array.Empty<string>(), names = Array.Empty<string>();
    public OutfitRating rating;
}
