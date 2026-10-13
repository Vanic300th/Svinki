using System;
using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using UnityEngine;

[Serializable] public struct LobbyStroke { public uint Id; public string Player; public Vector2[] Points; public byte Brush; public bool Erase; }
public sealed class DrawingBoard : LobbyInteractable
{
    [SerializeField] private Renderer surface;
    [SerializeField, Range(128, 1024)] private int resolution = 512;
    [SerializeField, Range(64, 1024)] private int maxCommands = 512;
    [SerializeField, Range(1024, 32768)] private int maxPoints = 8192;
    private readonly SyncList<LobbyStroke> strokes = new SyncList<LobbyStroke>();
    private readonly List<LobbyStroke> offlineStrokes = new List<LobbyStroke>();
    private readonly Dictionary<string, float> nextStroke = new Dictionary<string, float>();
    private Texture2D texture;
    private Color32[] pixels;
    private Material material;
    private int usedPoints;
    private uint sequence;
    public Texture2D Texture => texture;
    public int CommandCount => NetworkLobby.Instance?.Offline == true ? offlineStrokes.Count : strokes.Count;
    private void Awake()
    {
        texture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        texture.wrapMode = TextureWrapMode.Clamp; pixels = new Color32[resolution * resolution]; ClearTexture();
        material = surface.material; material.mainTexture = texture;
        strokes.OnChange += Changed;
    }
    public override void OnStartClient() { base.OnStartClient(); Rebuild(); }
    public override string Prompt(PlayerAvatar player) => "E — draw together · erase / clear";
    public override void Interact(PlayerAvatar player) => LobbyPanel.Open(this, player);
    public override void Apply(PlayerAvatar player, LobbyAction action, string text, int index, Vector2[] points)
    {
        string id = Identity(player);
        if (action == LobbyAction.ClearBoard)
        {
            // Clearing everyone else's strokes is a host action. Eraser remains available to everyone.
            if (NetworkLobby.Instance?.Offline != true && id != NetworkLobby.Instance.Snapshot.host) return;
            strokes.Clear(); offlineStrokes.Clear(); usedPoints = 0; sequence++; Rebuild(); return;
        }
        if (action != LobbyAction.Stroke || points == null || points.Length == 0 || points.Length > 32 ||
            CommandCount >= maxCommands || usedPoints + points.Length > maxPoints || index < 1 || index > 16) return;
        if (nextStroke.TryGetValue(id, out float deadline) && Time.unscaledTime < deadline) return;
        foreach (var point in points)
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return;
        nextStroke[id] = Time.unscaledTime + .09f;
        var command = new LobbyStroke { Id = ++sequence, Player = id, Points = points, Brush = (byte)index, Erase = text == "erase" };
        usedPoints += points.Length;
        if (NetworkLobby.Instance?.Offline == true) { offlineStrokes.Add(command); Paint(command); }
        else strokes.Add(command);
    }
    private void Changed(SyncListOperation operation, int index, LobbyStroke previous, LobbyStroke next, bool asServer)
    {
        if (operation == SyncListOperation.Add) Paint(next);
        else if (operation == SyncListOperation.Clear) ClearTexture();
    }
    private void ClearTexture() { for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(217, 217, 194, 255); Flush(); }
    private void Rebuild() { ClearTexture(); if (NetworkLobby.Instance?.Offline == true) foreach (var stroke in offlineStrokes) Paint(stroke); else foreach (var stroke in strokes) Paint(stroke); }
    private void Paint(LobbyStroke stroke)
    {
        for (int i = 0; i < stroke.Points.Length; i++)
        {
            Vector2 a = stroke.Points[Mathf.Max(0, i - 1)] * (resolution - 1), b = stroke.Points[i] * (resolution - 1);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int step = 0; step <= steps; step++)
            {
                Vector2 point = Vector2.Lerp(a, b, (float)step / steps);
                int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y), radius = stroke.Brush;
                for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                    if (dx * dx + dy * dy <= radius * radius && x + dx >= 0 && x + dx < resolution && y + dy >= 0 && y + dy < resolution)
                        pixels[(y + dy) * resolution + x + dx] = stroke.Erase ? new Color32(217, 217, 194, 255) : new Color32(27, 38, 33, 255);
            }
        }
        Flush();
    }
    private void Flush() { texture.SetPixels32(pixels); texture.Apply(false, false); }
    private void OnDestroy() { strokes.OnChange -= Changed; if (texture != null) Destroy(texture); if (material != null) Destroy(material); }
}
