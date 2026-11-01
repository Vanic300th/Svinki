using UnityEngine;

/// <summary>Local display preferences; never change the networked character or map coverage.</summary>
public static class HudDisplaySettings
{
    public const string MinimapKey = "svinki.hud.minimapScale";
    public const string PigKey = "svinki.hud.pigScale";
    public const float MinimumScale = .5f, MaximumScale = 1.4f;
    private static float? minimap, pig;
    public static float MinimapScale => minimap ??= Mathf.Clamp(PlayerPrefs.GetFloat(MinimapKey, .75f), MinimumScale, MaximumScale);
    public static float PigScale => pig ??= Mathf.Clamp(PlayerPrefs.GetFloat(PigKey, 1), MinimumScale, MaximumScale);
    public static void SetMinimap(float value) => Save(ref minimap, MinimapKey, value);
    public static void SetPig(float value) => Save(ref pig, PigKey, value);
    private static void Save(ref float? cache, string key, float value)
    {
        cache = Mathf.Clamp(value, MinimumScale, MaximumScale);
        PlayerPrefs.SetFloat(key, cache.Value); PlayerPrefs.Save();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { minimap = null; pig = null; }
}
