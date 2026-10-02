using FishNet;
using UnityEngine;

// A stacked room has its own physics world on the server.
public sealed class RoomPhysics : MonoBehaviour
{
    private void OnEnable()
    {
        if (InstanceFinder.TimeManager != null)
            InstanceFinder.TimeManager.OnPrePhysicsSimulation += Simulate;
    }

    private void OnDisable()
    {
        if (InstanceFinder.TimeManager != null)
            InstanceFinder.TimeManager.OnPrePhysicsSimulation -= Simulate;
    }

    private void Simulate(float deltaTime)
    {
        if (InstanceFinder.IsServerStarted)
        {
            var physicsScene = gameObject.scene.GetPhysicsScene();
            if (physicsScene.IsValid()) physicsScene.Simulate(deltaTime);
        }
    }
}
