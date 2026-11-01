using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Applies the approved text-only concept to the existing editable menu prefab.</summary>
public static class ProjectSvinTextMenuSetup
{
    [MenuItem("Svinki/UI/Apply text menu with soft ears")]
    public static void Apply()
    {
        var theme=AssetDatabase.LoadAssetAtPath<ProjectSvinTheme>(ProjectSvinUiSetup.ThemePath);
        if(!theme.labels.Any(t=>t.key=="Customize"))theme.labels=theme.labels.Concat(new[]{new ProjectSvinTheme.MenuText("Customize","Customize","Кастомайз")}).ToArray();
        theme.primary=ProjectSvinTheme.Hex("E8B4C1");theme.text=ProjectSvinTheme.Hex("F5E8DB");
        theme.primaryText=ProjectSvinTheme.Hex("39232E");theme.panel=ProjectSvinTheme.Hex("48313C");
        theme.muted=ProjectSvinTheme.Hex("D9BCC2");
        EditorUtility.SetDirty(theme);AssetDatabase.SaveAssetIfDirty(theme);
        var root=PrefabUtility.LoadPrefabContents(ProjectSvinUiSetup.MenuPath);
        try
        {
            var l=root.GetComponent<ProjectSvinMenuLayout>();
            var screen=(RectTransform)root.transform;screen.anchorMin=Vector2.zero;screen.anchorMax=Vector2.one;
            screen.offsetMin=screen.offsetMax=Vector2.zero;
            var background=(RectTransform)l.background.transform;
            var cover=background.GetComponent<UnityEngine.UI.AspectRatioFitter>()??background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            cover.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            if(theme.menuBackground!=null)cover.aspectRatio=theme.menuBackground.rect.width/theme.menuBackground.rect.height;
            SetRect(l.menuPanel.transform,110,40,740,1000);
            SetRect(l.projectTitle.transform,0,0,740,80);Font(l.projectTitle,52);
            SetRect(l.gameTitle.transform,0,72,740,175);Font(l.gameTitle,180);
            if(l.customizeButton==null)l.customizeButton=Object.Instantiate(l.mainButtons[4],l.menuPanel.transform,false);
            l.customizeButton.name="Customize";
            l.mainButtons[3].transform.SetParent(l.menuPanel.transform,false);
            UnityEngine.UI.Button[] home={l.playButton,l.customizeButton,l.mainButtons[4],l.mainButtons[3],l.mainButtons[5]};
            for(int i=0;i<home.Length;i++)TextButton(home[i],280+i*145, i==0?55:42,theme,i==4);
            GameLanguage.SetLiteral(l.customizeButton.GetComponentInChildren<TMP_Text>(true),theme.Label("Customize"));
            // Mode selection retains its existing callbacks, inputs, and checkpoint option.
            for(int i=0;i<3;i++)SetRect(l.mainButtons[i].transform,0,535+(i==0?1:i==1?0:i)*116,740,100);
            if(l.appearanceOverlay==null)
            {
                var overlay=NewRect("Customize overlay",root.transform,0,0,1920,1080);
                overlay.gameObject.AddComponent<UnityEngine.UI.Image>();
                l.appearanceOverlay=overlay.gameObject;
            }
            var wash=l.appearanceOverlay.GetComponent<UnityEngine.UI.Image>();wash.color=new Color(.09f,.055f,.075f,.82f);wash.sprite=null;
            var washStyle=wash.GetComponent<ProjectSvinElement>();
            if(washStyle!=null){washStyle.useThemeColor=false;washStyle.useThemeSprite=false;}
            var overlayRect=(RectTransform)l.appearanceOverlay.transform;overlayRect.anchorMin=Vector2.zero;overlayRect.anchorMax=Vector2.one;
            overlayRect.offsetMin=overlayRect.offsetMax=Vector2.zero;
            l.appearancePanel.transform.SetParent(l.appearanceOverlay.transform,false);
            SetRect(l.appearancePanel.transform,250,90,1420,900);
            var appearanceRect=(RectTransform)l.appearancePanel.transform;
            appearanceRect.anchorMin=appearanceRect.anchorMax=appearanceRect.pivot=Vector2.one*.5f;appearanceRect.anchoredPosition=Vector2.zero;
            var tag=l.appearanceTitle.transform.parent;SetRect(tag,40,20,1260,85);
            var tagImage=tag.GetComponent<UnityEngine.UI.Image>();if(tagImage!=null)Transparent(tagImage);
            SetRect(l.appearanceTitle.transform,0,0,1260,85);Font(l.appearanceTitle,44);
            var titleStyle=l.appearanceTitle.GetComponent<ProjectSvinElement>();titleStyle.role=ProjectSvinRole.Text;titleStyle.Apply();
            SetRect(l.previewRect,40,140,540,580);
            SetRect(l.rotateLeft.transform,40,748,142,68);SetRect(l.rotateRight.transform,438,748,142,68);
            SetRect(l.appearanceChoices,620,135,760,630);
            var scroll=l.appearanceChoices.GetComponent<UnityEngine.UI.ScrollRect>();SetRect(scroll.viewport,0,0,760,630);
            foreach(var t in new[]{l.rotateLeft,l.rotateRight})t.GetComponentInChildren<TMP_Text>(true).alignment=TextAlignmentOptions.Center;
            var hint=l.appearancePanel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="Scroll hint");
            if(hint!=null)SetRect(hint.transform,620,805,760,45);
            var hanger=l.appearancePanel.transform.Find("Hanger decoration");if(hanger!=null)hanger.gameObject.SetActive(false);
            if(l.appearanceClose==null)
            {
                l.appearanceClose=Object.Instantiate(l.playBackButton,l.appearancePanel.transform,false);
                l.appearanceClose.name="Close customization";
            }
            SetRect(l.appearanceClose.transform,1325,24,60,60);
            var closeText=l.appearanceClose.GetComponentInChildren<TMP_Text>(true);SetRect(closeText.transform,0,0,60,60);Font(closeText,38);closeText.alignment=TextAlignmentOptions.Center;
            GameLanguage.SetLiteral(closeText,"×");
            l.appearanceClose.gameObject.SetActive(true);l.appearanceOverlay.SetActive(false);
            // Preserve the moving artwork; the right half of the home screen remains clear.
            l.playOptionsPanel.SetActive(false);
            LanguageButtons(l);
            ProjectSvinMenuHoverSetup.Configure(root);
            PrefabUtility.SaveAsPrefabAsset(root,ProjectSvinUiSetup.MenuPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    [MenuItem("Svinki/UI/Fix footer language buttons")]
    public static void ApplyLanguageButtons()
    {
        var root=PrefabUtility.LoadPrefabContents(ProjectSvinUiSetup.MenuPath);
        try
        {
            LanguageButtons(root.GetComponent<ProjectSvinMenuLayout>());
            PrefabUtility.SaveAsPrefabAsset(root,ProjectSvinUiSetup.MenuPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static void LanguageButtons(ProjectSvinMenuLayout layout)
    {
        var separator=layout.transform.Find("Language").GetComponent<TMP_Text>();
        var theme=AssetDatabase.LoadAssetAtPath<ProjectSvinTheme>(ProjectSvinUiSetup.ThemePath);
        UnityEngine.UI.Button Make(UnityEngine.UI.Button button,string code,float x)
        {
            if(button==null)
            {
                var rect=NewRect(code+" language",layout.transform,0,0,76,54);
                var hit=rect.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;hit.raycastTarget=true;
                button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();
                var label=Object.Instantiate(separator,rect,false);label.name="Label";
                button.targetGraphic=label;
            }
            var area=(RectTransform)button.transform;
            area.anchorMin=area.anchorMax=area.pivot=Vector2.zero;area.anchoredPosition=new Vector2(x,20);area.sizeDelta=new Vector2(76,54);
            button.transition=UnityEngine.UI.Selectable.Transition.ColorTint;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=Color.white*1.15f;colors.pressedColor=new Color(.8f,.8f,.8f,1);colors.selectedColor=Color.white;button.colors=colors;
            var text=button.GetComponentInChildren<TMP_Text>(true);
            var r=text.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            Font(text,28);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            var style=text.GetComponent<ProjectSvinElement>()??text.gameObject.AddComponent<ProjectSvinElement>();
            style.theme=theme;style.fontRole=ProjectSvinFontRole.Heading;style.useThemeColor=false;style.Apply();text.color=theme.muted;
            GameLanguage.SetLiteral(text,code.ToUpperInvariant());
            return button;
        }
        layout.russianLanguageButton=Make(layout.russianLanguageButton,"ru",45);
        layout.englishLanguageButton=Make(layout.englishLanguageButton,"en",137);
        var sr=separator.rectTransform;sr.anchorMin=sr.anchorMax=sr.pivot=Vector2.zero;sr.anchoredPosition=new Vector2(121,29);sr.sizeDelta=new Vector2(16,36);
        separator.alignment=TextAlignmentOptions.Center;separator.raycastTarget=false;Font(separator,22);GameLanguage.SetLiteral(separator,"/");
    }
    private static void TextButton(UnityEngine.UI.Button button,float y,float fontSize,ProjectSvinTheme theme,bool exit)
    {
        SetRect(button.transform,0,y,620,108);
        var r=(RectTransform)button.transform;r.pivot=new Vector2(0,.5f);r.anchoredPosition=new Vector2(0,-y-54);
        button.transform.localScale=Vector3.one;button.transition=UnityEngine.UI.Selectable.Transition.None;
        Transparent(button.GetComponent<UnityEngine.UI.Image>());
        var icon=button.transform.Find("Icon — separate replaceable sprite");if(icon!=null)icon.gameObject.SetActive(false);
        var text=button.GetComponentInChildren<TMP_Text>(true);SetRect(text.transform,0,0,620,108);
        Font(text,fontSize);text.alignment=TextAlignmentOptions.MidlineLeft;
        var binding=text.GetComponent<ProjectSvinElement>();binding.fontRole=ProjectSvinFontRole.Heading;binding.useThemeColor=false;binding.Apply();text.color=theme.text;
        var hover=button.GetComponent<ProjectSvinMenuHover>()??button.gameObject.AddComponent<ProjectSvinMenuHover>();hover.label=text;
        hover.leftEar=Glyph("Hanging ear left",text.transform,false,false);hover.rightEar=Glyph("Hanging ear right",text.transform,true,false);
        if(hover.exitHoof!=null)hover.exitHoof.gameObject.SetActive(false);
        hover.RefreshEars();
    }
    private static ProjectSvinMenuGlyph Glyph(string name,Transform parent,bool mirror,bool hoof)
    {
        var child=parent.Find(name);var rect=child!=null?(RectTransform)child:NewRect(name,parent,0,0,44,60);
        var glyph=rect.GetComponent<ProjectSvinMenuGlyph>()??rect.gameObject.AddComponent<ProjectSvinMenuGlyph>();
        glyph.mirror=mirror;glyph.hoof=hoof;glyph.raycastTarget=false;glyph.color=ProjectSvinTheme.Current.primary;
        if(!hoof)glyph.canvasRenderer.SetAlpha(0);return glyph;
    }
    private static void Transparent(UnityEngine.UI.Image image)
    {
        if(image==null)return;
        var binding=image.GetComponent<ProjectSvinElement>();if(binding!=null){binding.useThemeColor=false;binding.useThemeSprite=false;binding.opacity=0;}
        image.color=Color.clear;image.sprite=null;
        foreach(var outline in image.GetComponents<UnityEngine.UI.Outline>())outline.enabled=false;
    }
    private static void Font(TMP_Text text,float size)
    {
        text.fontSize=text.fontSizeMin=text.fontSizeMax=size;text.enableAutoSizing=false;
        text.textWrappingMode=TextWrappingModes.NoWrap;text.fontStyle=FontStyles.Normal;
        var style=text.GetComponent<ProjectSvinElement>();if(style!=null)style.preserveFontSize=true;
    }
    private static RectTransform NewRect(string name,Transform parent,float x,float y,float width,float height)
    {var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);SetRect(obj.transform,x,y,width,height);return(RectTransform)obj.transform;}
    private static void SetRect(Transform target,float x,float y,float width,float height)
    {var rect=(RectTransform)target;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
}
