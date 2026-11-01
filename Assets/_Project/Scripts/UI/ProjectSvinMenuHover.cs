using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Transparent menu actions with soft folded ears and matching-size snout counters on hover.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Button))]
public sealed class ProjectSvinMenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public TMP_Text label;
    public TMP_FontAsset snoutFont;
    public ProjectSvinMenuGlyph leftEar, rightEar, exitHoof;
    [Range(1,1.15f)] public float hoverScale=1.07f;
    [Range(.05f,.5f)] public float transitionSeconds=.16f;
    public float leftOverlap=7, rightOverlap=7;
    public bool previewHover;
    private UnityEngine.UI.Button button;
    private bool pointer, selected;
    private float amount;
    private string previousText;
    private TMP_FontAsset previousFont;
    private TMP_FontAsset normalFont;
    private float previousSize;
    private Vector2 previousRect;
    private void OnEnable() { button=GetComponent<UnityEngine.UI.Button>(); previousText=null; }
    private void OnDisable()
    {
        pointer=selected=false; amount=0; transform.localScale=Vector3.one;
        if(label!=null&&normalFont!=null&&label.font==snoutFont)label.font=normalFont;
        if(leftEar!=null)leftEar.canvasRenderer.SetAlpha(0);
        if(rightEar!=null)rightEar.canvasRenderer.SetAlpha(0);
    }
    public void OnPointerEnter(PointerEventData data)=>pointer=true;
    public void OnPointerExit(PointerEventData data)=>pointer=false;
    public void OnSelect(BaseEventData data)=>selected=true;
    public void OnDeselect(BaseEventData data)=>selected=false;
    private void LateUpdate()
    {
        if(label==null)return;
        if(button==null)button=GetComponent<UnityEngine.UI.Button>();
        bool highlighted=button.IsInteractable()&&(Application.isPlaying?(pointer||selected):previewHover);
        amount=Application.isPlaying?Mathf.MoveTowards(amount,highlighted?1:0,Time.unscaledDeltaTime/Mathf.Max(.05f,transitionSeconds)):highlighted?1:0;
        float eased=amount*amount*(3-2*amount);
        // The alternate face has identical outlines, advances and kerning; only O's counter differs.
        if(label.font!=snoutFont)normalFont=label.font;
        if(normalFont==null&&ProjectSvinTheme.Current!=null)normalFont=ProjectSvinTheme.Current.headingFont;
        var desiredFont=eased>.5f&&snoutFont!=null?snoutFont:normalFont;
        if(desiredFont!=null&&label.font!=desiredFont)label.font=desiredFont;
        transform.localScale=Vector3.one*Mathf.Lerp(1,hoverScale,eased);
        var theme=ProjectSvinTheme.Current;
        Color normal=theme!=null?theme.text:Color.white, accent=theme!=null?theme.primary:new Color(.91f,.71f,.76f);
        Color tint=Color.Lerp(normal,accent,eased); if(!button.IsInteractable())tint.a*=.45f;
        label.color=tint; if(exitHoof!=null)exitHoof.color=tint;
        TintEar(leftEar,tint,eased); TintEar(rightEar,tint,eased);
        if(previousText!=label.text||previousFont!=label.font||previousSize!=label.fontSize||previousRect!=label.rectTransform.rect.size)
            RefreshEars();
    }
    private static void TintEar(ProjectSvinMenuGlyph ear,Color tint,float alpha)
    { if(ear!=null){ear.color=tint;ear.canvasRenderer.SetAlpha(alpha);} }
    public void RefreshEars()
    {
        if(label==null||!label.gameObject.activeInHierarchy)return;
        label.ForceMeshUpdate();
        previousText=label.text; previousFont=label.font; previousSize=label.fontSize; previousRect=label.rectTransform.rect.size;
        float left=float.PositiveInfinity,right=float.NegativeInfinity,top=float.NegativeInfinity;
        var info=label.textInfo;
        for(int i=0;i<info.characterCount;i++)
        {
            var ch=info.characterInfo[i]; if(!ch.isVisible)continue;
            // Use the ink bounds rather than the SDF quad's invisible padding.
            var metrics=ch.textElement.glyph.metrics;
            float inkLeft=ch.origin+metrics.horizontalBearingX*ch.scale;
            left=Mathf.Min(left,inkLeft); right=Mathf.Max(right,inkLeft+metrics.width*ch.scale);
            top=Mathf.Max(top,ch.baseLine+metrics.horizontalBearingY*ch.scale);
        }
        if(float.IsInfinity(left))return;
        Place(leftEar,new Vector2(left+leftOverlap,top-label.fontSize*.06f),new Vector2(1,.5f),0);
        Place(rightEar,new Vector2(right-rightOverlap,top-label.fontSize*.06f),new Vector2(0,.5f),0);
    }
    private void Place(ProjectSvinMenuGlyph ear,Vector2 point,Vector2 pivot,float angle)
    {
        if(ear==null)return;
        var rect=ear.rectTransform;rect.anchorMin=rect.anchorMax=label.rectTransform.pivot;rect.pivot=pivot;
        rect.anchoredPosition=point;rect.sizeDelta=Vector2.one*(label.fontSize*.7f);
        rect.localRotation=Quaternion.Euler(0,0,angle);ear.SetVerticesDirty();
    }
}
