using UnityEngine;
public sealed class AirHockeyGoal : MonoBehaviour
{
    [SerializeField] private AirHockeyTable table;
    [SerializeField] private bool sideA;
    private void OnTriggerEnter(Collider collider) => table.Goal(collider.GetComponentInParent<LobbyProp>(), sideA);
}
