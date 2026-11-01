using UnityEngine;

/// <summary>Quiet category silhouettes for empty inventory slots; no item names are shown.</summary>
public sealed class ClothingSlotGlyph : UnityEngine.UI.MaskableGraphic
{
    public ClothingSlot slot;
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
    {
        helper.Clear();
        switch (slot)
        {
            case ClothingSlot.Head:
                Quad(helper, .12f, .24f, .88f, .36f); Quad(helper, .29f, .35f, .71f, .77f); break;
            case ClothingSlot.Torso:
                Polygon(helper, new Vector2(.32f,.82f),new Vector2(.12f,.62f),new Vector2(.25f,.47f),new Vector2(.38f,.57f));
                Polygon(helper, new Vector2(.68f,.82f),new Vector2(.88f,.62f),new Vector2(.75f,.47f),new Vector2(.62f,.57f));
                Quad(helper,.32f,.16f,.68f,.76f); break;
            case ClothingSlot.Legs:
                Quad(helper,.25f,.58f,.75f,.83f); Quad(helper,.25f,.15f,.46f,.65f); Quad(helper,.54f,.15f,.75f,.65f); break;
            case ClothingSlot.Feet:
                Polygon(helper,new Vector2(.11f,.25f),new Vector2(.46f,.25f),new Vector2(.46f,.42f),new Vector2(.31f,.53f),new Vector2(.28f,.67f),new Vector2(.11f,.67f));
                Polygon(helper,new Vector2(.54f,.25f),new Vector2(.89f,.25f),new Vector2(.89f,.42f),new Vector2(.74f,.53f),new Vector2(.71f,.67f),new Vector2(.54f,.67f)); break;
        }
    }
    private void Quad(UnityEngine.UI.VertexHelper helper, float left, float bottom, float right, float top) =>
        Polygon(helper, new Vector2(left,bottom),new Vector2(right,bottom),new Vector2(right,top),new Vector2(left,top));
    private void Polygon(UnityEngine.UI.VertexHelper helper, params Vector2[] points)
    {
        int start = helper.currentVertCount; Rect rect = rectTransform.rect;
        foreach (var p in points) helper.AddVert(new Vector3(rect.x+p.x*rect.width,rect.y+p.y*rect.height),color,Vector2.zero);
        for (int i=1;i<points.Length-1;i++) helper.AddTriangle(start,start+i,start+i+1);
    }
}
