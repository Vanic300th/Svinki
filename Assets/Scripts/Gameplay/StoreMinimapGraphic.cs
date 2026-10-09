using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class StoreMinimapGraphic : UnityEngine.UI.MaskableGraphic
{
    // Show the player's neighbourhood: reduce the full-map viewing range by 70%.
    private const float VisibleRange = .30f;
    private StoreMap map;
    private Vector2 viewCenter;
    public void Bind(StoreMap value)
    {
        map = value;
        viewCenter = map != null ? (map.Minimum + map.Maximum) * .5f : Vector2.zero;
        SetVerticesDirty();
    }
    public void Follow(Vector3 world)
    {
        var center = new Vector2(world.x, world.z);
        if (center == viewCenter) return;
        viewCenter = center;
        SetVerticesDirty();
    }
    private float Scale => map == null ? 0 : Mathf.Min(
        rectTransform.rect.width / (map.Maximum.x - map.Minimum.x + 4),
        rectTransform.rect.height / (map.Maximum.y - map.Minimum.y + 4)) / VisibleRange;
    public Vector2 Project(Vector3 world)
    {
        if (map == null) return Vector2.zero;
        return (new Vector2(world.x, world.z) - viewCenter) * Scale;
    }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
    {
        vertices.Clear(); if (map == null) return;
        float scale = Scale;
        foreach (var floor in map.Floors) Box(vertices, Project(new Vector3(floor.center.x, 0, floor.center.y)), floor.size * scale, new Color(.16f, .25f, .30f), floor.angle);
        float lineWidth = canvas != null ? 2f / Mathf.Max(.1f, canvas.scaleFactor) : 2f;
        foreach (var wall in map.Walls) Box(vertices, Project(new Vector3(wall.center.x, 0, wall.center.y)), new Vector2(Mathf.Max(lineWidth, wall.size.x * scale), Mathf.Max(lineWidth, wall.size.y * scale)), new Color(.55f, .68f, .72f), wall.angle);
    }
    private static void Box(UnityEngine.UI.VertexHelper vh, Vector2 center, Vector2 size, Color tint, float angle)
    {
        int start = vh.currentVertCount; Vector2 h = size * .5f;
        var rotation=Quaternion.Euler(0,0,angle);
        vh.AddVert(center + (Vector2)(rotation * new Vector3(-h.x, -h.y)), tint, Vector2.zero);
        vh.AddVert(center + (Vector2)(rotation * new Vector3(-h.x, h.y)), tint, Vector2.zero);
        vh.AddVert(center + (Vector2)(rotation * new Vector3(h.x, h.y)), tint, Vector2.zero);
        vh.AddVert(center + (Vector2)(rotation * new Vector3(h.x, -h.y)), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
