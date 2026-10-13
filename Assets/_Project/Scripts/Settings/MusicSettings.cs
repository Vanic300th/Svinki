using UnityEngine;

public static class MusicSettings
{
    private const string Key = "svinki.music.volume";
    public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(Key, 1));
    public static void Set(float value)
    {
        PlayerPrefs.SetFloat(Key, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }
}
