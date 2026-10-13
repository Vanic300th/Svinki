using UnityEngine;
public sealed class LobbyMannequinAnomaly : LobbyAnomalyEvent
{
    [SerializeField] private Transform mannequin;
    [SerializeField] private Transform[] positions;
    public override void Apply(int variant)
    {
        if (positions.Length == 0 || mannequin == null) return;
        var place = positions[variant % positions.Length]; mannequin.SetPositionAndRotation(place.position, place.rotation);
    }
}
