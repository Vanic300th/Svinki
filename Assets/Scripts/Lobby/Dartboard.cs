using System.Collections.Generic;
using UnityEngine;

public sealed class Dartboard : LobbyScore
{
    [SerializeField, Min(.1f)] private float radius = .55f;
    private static readonly int[] sectors = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };
    private readonly HashSet<LobbyProp> stuck = new HashSet<LobbyProp>();
    public override string Prompt(PlayerAvatar player) => "E — return darts · throw toward the target";
    public static int ScoreAt(Vector2 point, float radius)
    {
        float normalized = point.magnitude / radius;
        if (normalized > 1) return 0;
        if (normalized < .037f) return 50;
        if (normalized < .094f) return 25;
        float angle = Mathf.Repeat(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg + 9, 360);
        int sector = sectors[Mathf.FloorToInt(angle / 18) % 20];
        return sector * (normalized >= .94f ? 2 : normalized >= .57f && normalized <= .63f ? 3 : 1);
    }
    public void Hit(LobbyProp dart, Vector3 point)
    {
        if (!Authority || dart == null || dart.Kind != LobbyPropKind.Dart || !dart.CanScore) return;
        Vector3 local = transform.InverseTransformPoint(point);
        Record(dart.LastThrower, ScoreAt(new Vector2(local.x, local.y), radius));
        dart.Stick(point - dart.transform.forward * .08f); stuck.Add(dart);
    }
    public override void Apply(PlayerAvatar player, LobbyAction action, string text, int index, Vector2[] points)
    {
        if (action != LobbyAction.Toggle && action != LobbyAction.Reset) return;
        foreach (var dart in stuck) if (dart != null && dart.Holder == null) dart.ResetHome();
        stuck.Clear();
    }
}
