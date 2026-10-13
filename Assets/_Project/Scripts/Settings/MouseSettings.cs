using UnityEngine;

public static class MouseSettings
{
    private const string Key = "svinki.mouse.sensitivity";
    private static float? cached;
    public static float Multiplier => cached ??= Mathf.Clamp(PlayerPrefs.GetFloat(Key, 1), .25f, 3);
    public static void Set(float value)
    {
        cached = Mathf.Clamp(value, .25f, 3);
        PlayerPrefs.SetFloat(Key, cached.Value);
        PlayerPrefs.Save();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => cached = null;
}
