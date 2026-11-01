using UnityEngine;
public sealed class LobbyMannequinAnomaly : LobbyAnomalyEvent
{
    [SerializeField] private Transform mannequin;
    [SerializeField] private Transform[] positions;
    public override int VariantCount => positions != null ? positions.Length : 0;
    public override void Apply(int variant)
    {
        if (positions == null || positions.Length == 0 || mannequin == null) return;
        var place = positions[(variant % positions.Length + positions.Length) % positions.Length];
        if (place != null) mannequin.SetPositionAndRotation(place.position, place.rotation);
    }
}
