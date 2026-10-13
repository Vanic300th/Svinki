using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

public sealed class GameWorldLocalizedText : MonoBehaviour
{
    public string Source;
    private TextMesh label;
    private LocalizeStringEvent localize;
    private float originalSize;
    private void Awake()
    {
        label=GetComponent<TextMesh>();originalSize=label.characterSize;
        localize=gameObject.AddComponent<LocalizeStringEvent>();localize.enabled=false;
        localize.OnUpdateString.AddListener(value=>
        {
            label.text=value;
            label.characterSize=originalSize*Mathf.Min(1,(float)Source.Length/Mathf.Max(1,value.Length));
        });
    }
    private void OnEnable()=>GameLanguage.Changed+=Refresh;
    private void OnDisable()=>GameLanguage.Changed-=Refresh;
    private void Start()=>Refresh();
    private void Refresh()
    {
        if (!GameLanguage.Ready || Source==null)return;
        localize.enabled=false;
        label.font=GameLanguage.Font.sourceFontFile;
        GetComponent<Renderer>().sharedMaterial=label.font.material;
        GetComponent<WorldTextDepth>()?.RefreshMaterial();
        localize.StringReference=new LocalizedString(GameLanguage.Table,GameLanguage.Key(Source));
        localize.enabled=true;
    }
}
