using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

public sealed class GameWorldLocalizedText : MonoBehaviour
{
    public string Source;
    private TextMesh label;
    private LocalizeStringEvent localize;
    private float originalSize;
    private float originalWidth;
    private void Awake()
    {
        label=GetComponent<TextMesh>();originalSize=label.characterSize;
        originalWidth=Width(label.font,label.text);
        localize=gameObject.AddComponent<LocalizeStringEvent>();localize.enabled=false;
        localize.OnUpdateString.AddListener(value=>
        {
            label.text=value;
            float width=Width(label.font,value);
            label.characterSize=originalSize*(originalWidth>0 && width>0 ? Mathf.Min(1,originalWidth/width) : 1);
        });
    }
    private void OnEnable()=>GameLanguage.Changed+=Refresh;
    private void OnDisable()=>GameLanguage.Changed-=Refresh;
    private void Start()=>Refresh();
    private void Refresh()
    {
        if (!GameLanguage.Ready || Source==null)return;
        localize.enabled=false;
        label.font=ProjectSvinTheme.Current?.worldHeadingFont != null ? ProjectSvinTheme.Current.worldHeadingFont : GameLanguage.Font.sourceFontFile;
        GetComponent<Renderer>().sharedMaterial=label.font.material;
        GetComponent<WorldTextDepth>()?.RefreshMaterial();
        localize.StringReference=new LocalizedString(GameLanguage.Table,GameLanguage.Key(Source));
        localize.enabled=true;
    }
    private float Width(Font font,string value)
    {
        if(font==null || string.IsNullOrEmpty(value))return 0;
        int size=label.fontSize>0?label.fontSize:32;
        font.RequestCharactersInTexture(value,size,label.fontStyle);
        float widest=0,line=0;
        foreach(char c in value)
        {
            if(c=='\n'){widest=Mathf.Max(widest,line);line=0;}
            else if(font.GetCharacterInfo(c,out var info,size,label.fontStyle))line+=info.advance;
        }
        return Mathf.Max(widest,line);
    }

}
