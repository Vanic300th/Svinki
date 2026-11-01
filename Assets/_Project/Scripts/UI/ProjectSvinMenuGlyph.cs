using UnityEngine;

/// <summary>Editable, resolution-independent menu ears and the split hoof exit mark.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class ProjectSvinMenuGlyph : UnityEngine.UI.MaskableGraphic
{
    public bool hoof, mirror;
    [Range(.5f,2)] public float edgeSoftness=1;
    private static readonly Vector2[] Ear = Curve(new Vector2(.66f,.88f),
        new Vector2(.44f,1.08f),new Vector2(.08f,.89f),new Vector2(.03f,.60f),
        new Vector2(-.02f,.40f),new Vector2(-.02f,.18f),new Vector2(.06f,.02f),
        new Vector2(.37f,.06f),new Vector2(.62f,.19f),new Vector2(.73f,.49f),
        new Vector2(.83f,.65f),new Vector2(.82f,.79f),new Vector2(.66f,.88f));
    private static readonly Vector2[] Fold = Curve(new Vector2(.65f,.90f),
        new Vector2(.85f,.79f),new Vector2(1,.46f),new Vector2(.99f,.24f),
        new Vector2(.90f,.18f),new Vector2(.75f,.29f),new Vector2(.70f,.43f),
        new Vector2(.63f,.57f),new Vector2(.62f,.75f),new Vector2(.65f,.90f));
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
    {
        vertices.Clear();
        if (hoof)
        {
            Polygon(vertices, new[]{new Vector2(.03f,.12f),new Vector2(.05f,.8f),new Vector2(.19f,.94f),new Vector2(.36f,.89f),new Vector2(.44f,.1f)}, color);
            Polygon(vertices, new[]{new Vector2(.55f,.08f),new Vector2(.59f,.9f),new Vector2(.75f,1),new Vector2(.92f,.94f),new Vector2(.98f,.12f)}, color);
            return;
        }
        Polygon(vertices, Ear, color);
        Polygon(vertices, Fold, color);
    }
    private static Vector2[] Curve(Vector2 start,params Vector2[] controls)
    {
        const int steps=12;
        var points=new Vector2[1+controls.Length/3*steps];points[0]=start;int index=1;
        for(int curve=0;curve<controls.Length;curve+=3)
        {
            for(int step=1;step<=steps;step++)
            {
                float t=step/(float)steps,u=1-t;
                points[index++]=u*u*u*start+3*u*u*t*controls[curve]+3*u*t*t*controls[curve+1]+t*t*t*controls[curve+2];
            }
            start=controls[curve+2];
        }
        return points;
    }
    private void Polygon(UnityEngine.UI.VertexHelper vertices, Vector2[] points, Color tint)
    {
        int count=points.Length;
        if((points[0]-points[count-1]).sqrMagnitude<.000001f)count--;
        int start=vertices.currentVertCount; Rect rect=GetPixelAdjustedRect();
        var positions=new Vector2[count];
        for(int i=0;i<count;i++)
        {
            var point=points[i];
            float x=mirror?1-point.x:point.x;
            positions[i]=new Vector2(rect.x+x*rect.width,rect.y+point.y*rect.height);
            vertices.AddVert(positions[i],tint,Vector2.zero);
        }
        for(int i=1;i<count-1;i++)vertices.AddTriangle(start,start+i,start+i+1);
        // A one-screen-pixel transparent fringe smooths overlay UI independently of camera MSAA.
        Camera view=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        var origin=RectTransformUtility.WorldToScreenPoint(view,rectTransform.TransformPoint(Vector3.zero));
        var unit=RectTransformUtility.WorldToScreenPoint(view,rectTransform.TransformPoint(Vector3.right));
        float width=edgeSoftness/Mathf.Max(.01f,Vector2.Distance(origin,unit));
        float area=0;for(int i=0;i<count;i++){var a=positions[i];var b=positions[(i+1)%count];area+=a.x*b.y-b.x*a.y;}
        Vector2 Normal(Vector2 edge)=>area>0?new Vector2(edge.y,-edge.x).normalized:new Vector2(-edge.y,edge.x).normalized;
        var transparent=tint;transparent.a=0;
        for(int i=0;i<count;i++)
        {
            var a=Normal(positions[i]-positions[(i+count-1)%count]);
            var b=Normal(positions[(i+1)%count]-positions[i]);var outward=(a+b).normalized;
            float miter=Mathf.Min(width*2,width/Mathf.Max(.5f,Vector2.Dot(outward,b)));
            vertices.AddVert(positions[i]+outward*miter,transparent,Vector2.zero);
        }
        for(int i=0;i<count;i++)
        {
            int next=(i+1)%count;
            vertices.AddTriangle(start+i,start+count+i,start+count+next);
            vertices.AddTriangle(start+i,start+count+next,start+next);
        }
    }
}
