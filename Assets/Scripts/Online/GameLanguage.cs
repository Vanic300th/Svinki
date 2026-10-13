using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

/// <summary>Local UI translations. The Unity locale is the only language state.</summary>
public static class GameLanguage
{
    public const string PreferenceKey = "svinki.language";
    public const string Table = "GameUI";
    public static event Action Changed;
    public static bool Ready { get; internal set; }
    public static TMP_FontAsset Font { get; internal set; }
    public static string Code => LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";
    [Serializable] public class Entry { public string source, translation; public bool translateArguments; }
    [Serializable] public class Catalog { public Entry[] entries; }
    private static Dictionary<string, Entry> exact;
    private static List<(Entry entry, Regex regex)> patterns;
    private static readonly Dictionary<string, string> resolved = new Dictionary<string, string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Changed = null; Ready = false; Font = null; exact = null; patterns = null; resolved.Clear(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => new GameObject("Game language").AddComponent<GameLanguageLoader>();
    public static string Key(string source)
    {
        // Stable keys retain identity when a translation is edited.
        ulong hash = 14695981039346656037;
        foreach (byte b in Encoding.UTF8.GetBytes(source)) { hash ^= b; hash *= 1099511628211; }
        return "ui_" + hash.ToString("x16");
    }
    private static void EnsureIndex()
    {
        if (exact != null) return;
        var asset = Resources.Load<TextAsset>("GameLanguageIndex");
        var data = JsonUtility.FromJson<Catalog>(asset.text);
        exact = data.entries.ToDictionary(e => e.source, StringComparer.Ordinal);
        patterns = data.entries.Where(e => e.source.Contains("{0}"))
            .OrderByDescending(e => e.source.Length).Select(e =>
            {
                string expression = Regex.Escape(e.source);
                for (int i = 0; i < 10; i++) expression = expression.Replace(Regex.Escape("{" + i + "}"), "(?<a" + i + ">.*?)");
                if (e.source == "{0}. {1}") expression = expression.Replace("(?<a0>.*?)", "(?<a0>\\d+)");
                return (e, new Regex("\\A" + expression + "\\z", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30)));
            }).ToList();
    }
    internal static bool Reference(string source, out string key, out object[] arguments)
    {
        EnsureIndex(); arguments = null;
        if (exact.ContainsKey(source)) { key = Key(source); return true; }
        foreach (var pattern in patterns)
        {
            var match = pattern.regex.Match(source);
            if (!match.Success) continue;
            key = Key(pattern.entry.source);
            arguments = Enumerable.Range(0, 10).TakeWhile(i => match.Groups["a" + i].Success)
                .Select(i => (object)(pattern.entry.translateArguments ? Words(match.Groups["a" + i].Value) : match.Groups["a" + i].Value)).ToArray();
            return true;
        }
        key = "passthrough"; arguments = new object[] { source }; return false;
    }
    internal static bool HasSource(string source) { EnsureIndex(); return source != null && exact.ContainsKey(source); }
    private static string Words(string value) => string.Join(", ", value.Split(new[] { ", " }, StringSplitOptions.None).Select(Text));
    public static string Text(string source)
    {
        if (string.IsNullOrEmpty(source) || !Ready) return source ?? "";
        if (resolved.TryGetValue(source, out var cached)) return cached;
        string result;
        if (Reference(source, out var key, out var arguments))
            result = LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, arguments);
        else if (source.Contains("\n")) result = string.Join("\n", source.Split('\n').Select(Text));
        else result = source;
        // Only cache static values; counts and player names must not grow this dictionary forever.
        if (exact.ContainsKey(source)) resolved[source] = result;
        return result;
    }
    public static void Set(TMP_Text label, string source)
    {
        if (label == null) return;
        var binding = label.GetComponent<GameLocalizedText>();
        if (binding == null) binding = label.gameObject.AddComponent<GameLocalizedText>();
        binding.Set(source ?? "");
    }
    public static void SetLiteral(TMP_Text label, string value)
    {
        var binding = label.GetComponent<GameLocalizedText>() ?? label.gameObject.AddComponent<GameLocalizedText>();
        binding.Set(value ?? "", true);
    }
    public static void Select(string code)
    {
        if (code != "en" && code != "ru") return;
        var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
        if (locale == null) return;
        PlayerPrefs.SetString(PreferenceKey, code); PlayerPrefs.Save();
        LocalizationSettings.SelectedLocale = locale;
    }
    internal static void Notify() { resolved.Clear(); Changed?.Invoke(); }
}

[Serializable]
public sealed class SavedGameLocaleSelector : IStartupLocaleSelector
{
    public Locale GetStartupLocale(ILocalesProvider locales) => locales.GetLocale(PlayerPrefs.GetString(GameLanguage.PreferenceKey, "en")) ?? locales.GetLocale("en");
}
