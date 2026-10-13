using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public sealed class GameLanguageLoader : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject); LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += BindSceneLabels;
    }
    private IEnumerator Start() { yield return LocalizationSettings.InitializationOperation; yield return Load(); }
    private void LocaleChanged(Locale locale) { GameLanguage.Ready = false; StartCoroutine(Load()); }
    private IEnumerator Load()
    {
        var locale = LocalizationSettings.SelectedLocale;
        var request = LocalizationSettings.StringDatabase.GetTableAsync(GameLanguage.Table, locale);
        yield return request;
        var font = LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<TMPro.TMP_FontAsset>("GameAssets","ui.font",locale);
        yield return font;
        if (locale != LocalizationSettings.SelectedLocale) yield break;
        if (request.Result == null) { Debug.LogError("Game language table could not be loaded."); yield break; }
        if (font.Result == null) { Debug.LogError("Bilingual game font could not be loaded."); yield break; }
        GameLanguage.Font = font.Result;
        GameLanguage.Ready = true; GameLanguage.Notify();
    }
    private void BindSceneLabels(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (text.GetComponent<GameLocalizedText>() == null && GameLanguage.HasSource(text.text)) GameLanguage.Set(text,text.text);
            foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
                if (GameLanguage.HasSource(text.text)) text.gameObject.AddComponent<GameWorldLocalizedText>().Source = text.text;
        }
    }
    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= BindSceneLabels;
    }
}
