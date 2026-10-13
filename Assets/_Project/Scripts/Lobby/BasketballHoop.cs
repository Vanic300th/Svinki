using System.Collections.Generic;
using UnityEngine;

public sealed class BasketballHoop : LobbyScore
{
    [SerializeField, Min(.1f)] private float scoringRadius = .22f;
    private readonly Dictionary<LobbyProp, (int serial, float at)> entered = new Dictionary<LobbyProp, (int, float)>();
    private readonly Dictionary<LobbyProp, int> scored = new Dictionary<LobbyProp, int>();
    private void Awake()
    {
        // The flat backboard top and the narrow gap above the rear rim can
        // support a stationary ball. Sloped collision caps shed it forward.
        Deflector("Backboard top deflector", new Vector3(0, 3.9f, .25f), new Vector3(1.82f, .04f, .3f));
        Deflector("Rear rim deflector", new Vector3(0, 2.93f, .11f), new Vector3(.9f, .05f, .65f));
    }
    private void Deflector(string objectName, Vector3 position, Vector3 size)
    {
        if (transform.Find(objectName) != null) return;
        var surface = new GameObject(objectName);
        surface.transform.SetParent(transform, false);
        surface.transform.localPosition = position;
        surface.transform.localRotation = Quaternion.Euler(-35, 0, 0);
        surface.AddComponent<BoxCollider>().size = size;
    }
    public override string Prompt(PlayerAvatar player) => "Throw a basketball through the hoop from above";
    public override void Apply(PlayerAvatar player, LobbyAction action, string text, int index, Vector2[] points) { }
    public void Cross(LobbyProp ball, bool upper)
    {
        if (!Authority || ball == null || ball.Kind != LobbyPropKind.Basketball || !ball.CanScore ||
            ball.GetComponent<Rigidbody>().linearVelocity.y >= -.05f) return;
        Vector3 point = transform.InverseTransformPoint(ball.transform.position);
        if (new Vector2(point.x, point.z + .35f).sqrMagnitude > scoringRadius * scoringRadius) return;
        if (upper) entered[ball] = (ball.ThrowSerial, Time.unscaledTime);
        else if (entered.TryGetValue(ball, out var pass) && pass.serial == ball.ThrowSerial && Time.unscaledTime - pass.at < 1.2f &&
            (!scored.TryGetValue(ball, out var previous) || previous != ball.ThrowSerial))
        {
            scored[ball] = ball.ThrowSerial; entered.Remove(ball);
            var value = Score; Record(ball.LastThrower, value.Player == ball.LastThrower ? value.Last + 1 : 1);
        }
    }
}
