using UnityEngine;
public sealed class BasketballScoreGate : MonoBehaviour
{
    [SerializeField] private BasketballHoop hoop;
    [SerializeField] private bool upper;
    private void OnTriggerEnter(Collider other) => hoop.Cross(other.GetComponentInParent<LobbyProp>(), upper);
}
