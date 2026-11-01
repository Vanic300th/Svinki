using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Targeted migration of the editable menu; retains the existing action and appearance references.</summary>
public static class ProjectSvinMenuRevision
{
    [MenuItem("Svinki/UI/Apply compact menu and readable controls")]
    public static void Apply()
    {
        ProjectSvinTypographySetup.Apply();
        var theme = AssetDatabase.LoadAssetAtPath<ProjectSvinTheme>(ProjectSvinUiSetup.ThemePath);
        AddLabel(theme,"Play","Play","Играть"); AddLabel(theme,"Back","← Back","← Назад");
        theme.appearanceControlScale = 1.5f;
        EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme);
        var root = PrefabUtility.LoadPrefabContents(ProjectSvinUiSetup.MenuPath);
        try
        {
            var l = root.GetComponent<ProjectSvinMenuLayout>();
            SetRect(l.menuPanel.transform,100,40,780,1000);
            SetRect(l.projectTitle.transform,0,20,780,88); Font(l.projectTitle,69,ProjectSvinFontRole.Brand);
            SetRect(l.gameTitle.transform,0,105,780,200); Font(l.gameTitle,162,ProjectSvinFontRole.Logo);
            if(l.playOptionsPanel==null)l.playOptionsPanel=NewRect("Play options",l.menuPanel.transform,0,0,780,1000).gameObject;
            if(l.playButton==null)l.playButton=UnityEngine.Object.Instantiate(l.mainButtons[0],l.menuPanel.transform,false);
            l.playButton.name="Play"; SetRect(l.playButton.transform,0,410,740,114);
            ButtonLabel(l.playButton,36,114); SetButtonIcon(l.playButton,theme.actionIcons[2],true);
            GameLanguage.SetLiteral(l.playButton.GetComponentInChildren<TMP_Text>(),theme.Label("Play"));
            if(l.playBackButton==null)l.playBackButton=UnityEngine.Object.Instantiate(l.mainButtons[3],l.playOptionsPanel.transform,false);
            l.playBackButton.name="Back to main menu";SetRect(l.playBackButton.transform,0,315,255,55);
            ButtonLabel(l.playBackButton,22,55);HideIcon(l.playBackButton);
            GameLanguage.SetLiteral(l.playBackButton.GetComponentInChildren<TMP_Text>(),theme.Label("Back"));
            foreach(var t in new[]{l.nickname.transform,l.joinCode.transform,l.continueButton.transform})t.SetParent(l.playOptionsPanel.transform,false);
            SetRect(l.nickname.transform,0,385,355,80);SetRect(l.joinCode.transform,385,385,355,80);
            foreach(var input in new[]{l.nickname,l.joinCode})
            {
                SetRect(input.textViewport,16,4,323,72);input.pointSize=32;
                Font(input.textComponent,32,ProjectSvinFontRole.Input);input.textComponent.textWrappingMode=TextWrappingModes.NoWrap;
                if(input.placeholder is TMP_Text placeholder)
                {Font(placeholder,28.5f,ProjectSvinFontRole.Input);placeholder.textWrappingMode=TextWrappingModes.NoWrap;placeholder.overflowMode=TextOverflowModes.Ellipsis;}
            }
            SetRect(l.status.transform,0,472,740,55);
            for(int i=0;i<4;i++)
            {
                var b=l.mainButtons[i];b.transform.SetParent(l.playOptionsPanel.transform,false);
                SetRect(b.transform,0,i<3?535+i*116:881,740,i<3?100:52);
                ButtonLabel(b,i<3?30:21,i<3?100:52);SetButtonIcon(b,theme.actionIcons[i],i==0);
            }
            SetRect(l.mainButtons[4].transform,0,550,740,104);ButtonLabel(l.mainButtons[4],34,104);SetButtonIcon(l.mainButtons[4],theme.actionIcons[4],false);
            SetRect(l.mainButtons[5].transform,0,680,740,104);ButtonLabel(l.mainButtons[5],34,104);SetButtonIcon(l.mainButtons[5],theme.actionIcons[5],false);
            SetRect(l.continueButton.transform,0,945,740,48);ButtonLabel(l.continueButton,19,48);HideIcon(l.continueButton);
            SetRect(l.roster.transform,0,400,740,250);SetRect(l.sessionActions,0,670,740,300);
            SetRect(l.actionTemplate.transform,0,0,740,72);ButtonLabel(l.actionTemplate,25,72);
            SetRect(l.appearancePanel.transform,1030,70,820,940);
            var tag=l.appearanceTitle.transform.parent;SetRect(tag,30,-25,760,105);
            SetRect(l.appearanceTitle.transform,24,0,712,105);Font(l.appearanceTitle,52.5f,ProjectSvinFontRole.Heading);
            SetRect(l.previewRect,230,90,360,300);
            SetRect(l.rotateLeft.transform,30,310,142.5f,67.5f);ButtonLabel(l.rotateLeft,38,67.5f);
            SetRect(l.rotateRight.transform,647.5f,310,142.5f,67.5f);ButtonLabel(l.rotateRight,38,67.5f);
            RemovePresets(l);
            SetRect(l.appearanceChoices,30,415,760,445);
            var scroll=l.appearanceChoices.GetComponent<UnityEngine.UI.ScrollRect>();
            SetRect(scroll.viewport,0,0,760,445);scroll.content.sizeDelta=Vector2.zero;
            var hit=scroll.viewport.GetComponent<UnityEngine.UI.Image>()??scroll.viewport.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;hit.raycastTarget=true;
            scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing=13.5f;
            scroll.scrollSensitivity=54;
            SetRect(l.appearanceChoiceTemplate.transform,30,415,760,69);
            var choiceText=l.appearanceChoiceTemplate.GetComponentInChildren<TMP_Text>();
            SetRect(choiceText.transform,21,0,718,69);Font(choiceText,27,ProjectSvinFontRole.Heading);FitLabel(choiceText,27);
            var hint=l.appearancePanel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="Scroll hint");
            if(hint!=null){SetRect(hint.transform,30,879,760,45);Font(hint,21,ProjectSvinFontRole.Body);FitLabel(hint,21);}
            var backdrop=(RectTransform)l.background.transform;backdrop.anchorMin=backdrop.anchorMax=backdrop.pivot=Vector2.one*.5f;backdrop.anchoredPosition=Vector2.zero;backdrop.sizeDelta=new Vector2(1920,1080);
            if(l.background.GetComponent<ProjectSvinBackgroundMotion>()==null)l.background.AddComponent<ProjectSvinBackgroundMotion>();
            l.playOptionsPanel.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,ProjectSvinUiSetup.MenuPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    [MenuItem("Svinki/UI/Remove appearance presets")]
    public static void RemoveAppearancePresets()
    {
        var root=PrefabUtility.LoadPrefabContents(ProjectSvinUiSetup.MenuPath);
        try
        {
            var l=root.GetComponent<ProjectSvinMenuLayout>();
            RemovePresets(l);
            SetRect(l.appearanceChoices,30,415,760,445);
            var scroll=l.appearanceChoices.GetComponent<UnityEngine.UI.ScrollRect>();
            SetRect(scroll.viewport,0,0,760,445);
            scroll.content.anchoredPosition=Vector2.zero;
            scroll.verticalNormalizedPosition=1;
            var hint=l.appearancePanel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="Scroll hint");
            if(hint!=null){Font(hint,21,ProjectSvinFontRole.Body);FitLabel(hint,21);}
            SetRect(l.appearanceChoiceTemplate.transform,30,415,760,69);
            PrefabUtility.SaveAsPrefabAsset(root,ProjectSvinUiSetup.MenuPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static void RemovePresets(ProjectSvinMenuLayout layout)
    {
        foreach(var button in layout.appearancePresets)
            if(button!=null)UnityEngine.Object.DestroyImmediate(button.gameObject);
        layout.appearancePresets=new UnityEngine.UI.Button[0];
    }
    [MenuItem("Svinki/UI/Fit menu button labels")]
    public static void FitButtonLabels()
    {
        var root=PrefabUtility.LoadPrefabContents(ProjectSvinUiSetup.MenuPath);
        try
        {
            var l=root.GetComponent<ProjectSvinMenuLayout>();
            FitLabel(l.playButton.GetComponentInChildren<TMP_Text>(true),36);
            FitLabel(l.playBackButton.GetComponentInChildren<TMP_Text>(true),22);
            float[] sizes={30,30,30,21,34,34};
            for(int i=0;i<l.mainButtons.Length;i++)FitLabel(l.mainButtons[i].GetComponentInChildren<TMP_Text>(true),sizes[i]);
            FitLabel(l.continueButton.GetComponentInChildren<TMP_Text>(true),19);
            FitLabel(l.actionTemplate.GetComponentInChildren<TMP_Text>(true),25);
            foreach(var b in new[]{l.rotateLeft,l.rotateRight})FitLabel(b.GetComponentInChildren<TMP_Text>(true),38);
            foreach(var b in l.appearancePresets)FitLabel(b.GetComponentInChildren<TMP_Text>(true),24);
            FitLabel(l.appearanceChoiceTemplate.GetComponentInChildren<TMP_Text>(true),27);
            PrefabUtility.SaveAsPrefabAsset(root,ProjectSvinUiSetup.MenuPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static void FitLabel(TMP_Text text,float size)
    {
        text.fontSize=text.fontSizeMax=size;text.fontSizeMin=size*.7f;text.enableAutoSizing=true;
        text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;
        text.GetComponent<ProjectSvinElement>().preserveFontSize=true;
    }
    private static void AddLabel(ProjectSvinTheme theme,string key,string en,string ru)
    {
        if(!theme.labels.Any(l=>l.key==key))theme.labels=theme.labels.Concat(new[]{new ProjectSvinTheme.MenuText(key,en,ru)}).ToArray();
    }
    private static void Font(TMP_Text text,float size,ProjectSvinFontRole role)
    {
        var binding=text.GetComponent<ProjectSvinElement>();binding.fontRole=role;binding.preserveFontSize=true;binding.Apply();
        text.fontSize=text.fontSizeMin=text.fontSizeMax=size;text.enableAutoSizing=false;text.fontStyle=FontStyles.Normal;
    }
    private static void ButtonLabel(UnityEngine.UI.Button button,float size,float height)
    {
        var text=button.GetComponentInChildren<TMP_Text>(true);bool icon=button.transform.Find("Icon — separate replaceable sprite")!=null;
        float x=icon?94:18;SetRect(text.transform,x,0,((RectTransform)button.transform).sizeDelta.x-x-22,height);Font(text,size,ProjectSvinFontRole.Heading);FitLabel(text,size);
    }
    private static void SetButtonIcon(UnityEngine.UI.Button button,Sprite sprite,bool primary)
    {
        var icon=button.transform.Find("Icon — separate replaceable sprite");if(icon==null)return;
        icon.gameObject.SetActive(true);SetRect(icon,26,(((RectTransform)button.transform).sizeDelta.y-45)*.5f,45,45);
        var image=icon.GetComponent<UnityEngine.UI.Image>();image.sprite=sprite;
    }
    private static void HideIcon(UnityEngine.UI.Button button)
    {
        var icon=button.transform.Find("Icon — separate replaceable sprite");if(icon!=null)icon.gameObject.SetActive(false);
        var text=button.GetComponentInChildren<TMP_Text>(true);SetRect(text.transform,18,0,((RectTransform)button.transform).sizeDelta.x-36,((RectTransform)button.transform).sizeDelta.y);
    }
    private static RectTransform NewRect(string name,Transform parent,float x,float y,float w,float h)
    {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);SetRect(go.transform,x,y,w,h);return(RectTransform)go.transform;}
    private static void SetRect(Transform target,float x,float y,float w,float h)
    {var r=(RectTransform)target;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
}
