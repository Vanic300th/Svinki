using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class OutfitRatingCheck
{
    [Serializable] public class Example { public string name; public OutfitRating rating; }
    [Serializable] public class Report { public int combinations, min, max; public Example[] references; public Example mixed, clash; public string[] checks; }
    static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    public static object Main()
    {
        var rules = OutfitRatingCatalog.Load();
        Check(rules != null && rules.items.Length == 33 && rules.references.Length == 19, "Expected 33 garments and 19 unique references.");
        Check(rules.items.Select(p => p.clothing).Distinct().Count() == 33, "Repeated metadata.");
        var references = rules.references.Select(r => new Example { name = r.name, rating = rules.Evaluate(r.items) }).ToArray();
        Check(references.All(r => r.rating.score >= 95), "A supplied outfit must score highly.");
        var groups = Enumerable.Range(0, 4).Select(i => rules.items.Where(p => (int)p.clothing.Slot == i).Select(p => p.clothing).ToArray()).ToArray();
        var ratings = (from h in groups[0] from t in groups[1] from l in groups[2] from f in groups[3]
            select new[] { h, t, l, f }).Select(outfit =>
        {
            var rating = rules.Evaluate(outfit);
            Check(rating.score >= 0 && rating.score <= 100 && rating.completeness == 100 && rating.ratedItems == 4, "Invalid full-outfit result.");
            Check(JsonUtility.ToJson(rating) == JsonUtility.ToJson(rules.Evaluate(outfit.Reverse())), "Input order changed the result.");
            return rating.score;
        }).ToArray();
        Check(ratings.Length == 2800, "Expected all 2800 combinations.");
        Check(rules.Evaluate(null).score == 0, "Empty outfit is not zero.");
        for (int n = 1; n < 4; n++) Check(rules.Evaluate(rules.references[0].items.Take(n)).score <= n * 25, "Partial outfit score inflated.");
        var one = rules.references[0].items[0];
        Check(rules.Evaluate(new[] { one, one, one, one }).score <= 25, "Duplicate slots inflated score.");
        var unknown = ScriptableObject.CreateInstance<HatClothing>();
        try { Check(rules.Evaluate(new[] { unknown }).score == 0 && rules.Evaluate(new[] { unknown }).ratedItems == 0, "Unknown garment was rated."); }
        finally { UnityEngine.Object.DestroyImmediate(unknown); }
        ClothingDefinition[] Outfit(params string[] keys) => keys.Select(k => rules.items.Single(p => p.key == k).clothing).ToArray();
        var mixed = new Example { name = "Новый комплект: sweater1, pants2, hat1, sneakers2", rating = rules.Evaluate(Outfit("sweater1", "pants2", "hat1", "sneakers2")) };
        var clash = new Example { name = "Смешение: shirt3, pants2, hat2, loafers1", rating = rules.Evaluate(Outfit("shirt3", "pants2", "hat2", "loafers1")) };
        Check(mixed.rating.score >= 85, "Old mixed outfit lost compatibility.");
        var expanded = rules.Evaluate(Outfit("shirt7", "pants2", "hat1", "sneakers4"));
        Check(expanded.ratedItems == 4 && expanded.completeness == 100 && expanded.score > 0, "Mixing original and new clothes was not rated.");
        foreach (var profile in rules.items)
            foreach (var alias in profile.aliases)
            {
                Check(rules.Profile(alias) == profile && rules.FindClothing(alias.name) == alias, "An earlier clothing definition was lost.");
                Check(rules.Evaluate(new[] {alias}).score == rules.Evaluate(new[] {profile.clothing}).score, "An earlier definition scored differently.");
            }
        var signatures = rules.references.Select(r => string.Join("|", r.items.Select(c => c.name).OrderBy(n => n, StringComparer.Ordinal))).ToArray();
        Check(signatures.Distinct().Count() == rules.references.Length, "Repeated reference outfits.");
        var inventory = rules.references[0].items.ToList();
        var entry = OutfitRoundResults.Rate(rules, "test", "Свинка", 0, inventory);
        inventory.Clear(); Check(entry.clothing.Length == 4 && entry.rating.score >= 95, "Results changed after live inventory mutation.");
        var snapshot = new SessionSnapshot { phase = SessionPhase.Results, results = new OutfitRoundResults { round = 3, entries = new[] { entry } } };
        var copy = JsonUtility.FromJson<SessionSnapshot>(JsonUtility.ToJson(snapshot));
        Check(copy.results.entries[0].rating.score == entry.rating.score && copy.results.round == 3 && copy.results.entries[0].clothing.SequenceEqual(entry.clothing), "Network result serialization lost data.");
        var report = new Report { combinations = ratings.Length, min = ratings.Min(), max = ratings.Max(), references = references, mixed = mixed, clash = clash,
            checks = new[] { "reference outfits", "2800 combinations", "determinism", "partial outfits", "duplicate slots", "unknown garments", "mixed original and expanded wardrobe", "legacy definitions", "unique references", "immutable inventory snapshot", "network JSON roundtrip" } };
        Directory.CreateDirectory("ArtSource/OutfitRating");
        File.WriteAllText("ArtSource/OutfitRating/validation.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
