using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class StoreMinimapGraphic : UnityEngine.UI.Graphic
{
    private StoreMap map;
    public void Bind(StoreMap value) { map = value; SetVerticesDirty(); }
    public Vector2 Project(Vector3 world)
    {
        if (map == null) return Vector2.zero;
        float scale = Mathf.Min(rectTransform.rect.width / (map.Maximum.x - map.Minimum.x + 4), rectTransform.rect.height / (map.Maximum.y - map.Minimum.y + 4));
        return (new Vector2(world.x, world.z) - (map.Minimum + map.Maximum) * .5f) * scale;
    }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
    {
        vertices.Clear(); if (map == null) return;
        float scale = Mathf.Min(rectTransform.rect.width / (map.Maximum.x - map.Minimum.x + 4), rectTransform.rect.height / (map.Maximum.y - map.Minimum.y + 4));
        foreach (var floor in map.Floors) Box(vertices, Project(new Vector3(floor.center.x, 0, floor.center.y)), floor.size * scale, new Color(.16f, .25f, .30f));
        float lineWidth = canvas != null ? 2f / Mathf.Max(.1f, canvas.scaleFactor) : 2f;
        foreach (var wall in map.Walls) Box(vertices, Project(new Vector3(wall.center.x, 0, wall.center.y)), new Vector2(Mathf.Max(lineWidth, wall.size.x * scale), Mathf.Max(lineWidth, wall.size.y * scale)), new Color(.55f, .68f, .72f));
    }
    private static void Box(UnityEngine.UI.VertexHelper vh, Vector2 center, Vector2 size, Color tint)
    {
        int start = vh.currentVertCount; Vector2 h = size * .5f;
        vh.AddVert(center + new Vector2(-h.x, -h.y), tint, Vector2.zero);
        vh.AddVert(center + new Vector2(-h.x, h.y), tint, Vector2.zero);
        vh.AddVert(center + new Vector2(h.x, h.y), tint, Vector2.zero);
        vh.AddVert(center + new Vector2(h.x, -h.y), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
