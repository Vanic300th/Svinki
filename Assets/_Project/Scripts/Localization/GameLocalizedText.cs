using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

/// <summary>Preserves the canonical source even when an inactive panel changes locale.</summary>
[DisallowMultipleComponent]
public sealed class GameLocalizedText : MonoBehaviour
{
    private TMP_Text label;
    private LocalizeStringEvent localize;
    private string source;
    private bool direct;
    private bool literal;
    private void Awake() => Initialize();
    private void Initialize()
    {
        if (label != null) return;
        label = GetComponent<TMP_Text>();
        localize = GetComponent<LocalizeStringEvent>() ?? gameObject.AddComponent<LocalizeStringEvent>();
        localize.OnUpdateString.AddListener(Apply);
    }
    private void Start()
    {
        label.fontSizeMax = label.fontSize; label.fontSizeMin = label.fontSize * .7f; label.enableAutoSizing = true;
    }
    private void OnEnable() { GameLanguage.Changed += Refresh; if (source != null) Refresh(); }
    private void OnDisable() => GameLanguage.Changed -= Refresh;
    public void Set(string value, bool keepLiteral = false) { Initialize(); if (source == value && literal == keepLiteral) return; source = value; literal = keepLiteral; Refresh(); }
    private void Refresh()
    {
        if (source == null) return;
        localize.enabled = false;
        if (!GameLanguage.Ready || source.Length == 0) { label.text = source; return; }
        label.font = GameLanguage.Font;
        direct = GameLanguage.Reference(source, out string key, out object[] arguments);
        if (literal) { direct = true; key = "passthrough"; arguments = new object[] { source }; }
        localize.StringReference = new UnityEngine.Localization.LocalizedString(GameLanguage.Table,key) { Arguments = arguments };
        localize.enabled = true;
    }
    private void Apply(string value) => label.text = direct ? value : GameLanguage.Text(source);
}
