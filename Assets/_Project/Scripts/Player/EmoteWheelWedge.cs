using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class EmoteWheelWedge : MaskableGraphic
{
    public int Index;
    public int Count = 6;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); const int steps = 20;
        float segment = 360f / Count;
        float from = 90 - Index * segment - segment * .5f + 2, to = from + segment - 4;
        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.Lerp(from, to, (float)i / steps) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(direction * 106, color, Vector2.zero); mesh.AddVert(direction * 245, color, Vector2.zero);
            if (i == 0) continue;
            int n = i * 2; mesh.AddTriangle(n-2,n-1,n); mesh.AddTriangle(n,n-1,n+1);
        }
    }
}
