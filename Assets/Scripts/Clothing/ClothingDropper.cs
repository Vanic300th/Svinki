using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Returns a worn garment to the level, reusing its hidden pickup whenever possible.</summary>
public static class ClothingDropper
{
    private static readonly RaycastHit[] hits = new RaycastHit[24];
    public static bool TryDrop(PlayerAvatar player, ClothingSlot slot, NetworkManager server = null)
    {
        if (player == null || player.GetComponent<PlayerKnockdown>()?.IsDown == true) return false;
        var clothing = player.Outfit.Get(slot); if (clothing == null) return false;
        var pickup = ClothingPickup.FindPickedUp(clothing, player.gameObject.scene);
        if (pickup == null && clothing.PickupPrefab == null) return false;
        if (!FloorPoint(player, slot, out Vector3 floor)) return false;
        if (pickup == null)
        {
            var obj = Object.Instantiate(clothing.PickupPrefab, floor + Vector3.up * .025f, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(obj, player.gameObject.scene);
            pickup = obj.GetComponent<ClothingPickup>();
            if (server != null) server.ServerManager.Spawn(obj.GetComponent<NetworkObject>(), null, player.gameObject.scene);
            else { obj.GetComponent<NetworkPickup>().enabled = false; obj.GetComponent<NetworkObject>().enabled = false; }
        }
        if (!player.Outfit.Remove(slot, "Dropped", out _)) return false;
        pickup.PlaceAt(floor); pickup.ProtectFromThieves(3);
        return true;
    }
    private static bool FloorPoint(PlayerAvatar player, ClothingSlot slot, out Vector3 floor)
    {
        var physics = player.gameObject.scene.GetPhysicsScene();
        Vector3 forward = player.transform.forward;
        if (player.TryGetView(out PlayerView view)) forward = view.Rotation * Vector3.forward;
        forward.y = 0; forward = forward.sqrMagnitude > .01f ? forward.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward) * (((int)slot - 1.5f) * .24f);
        Vector3 origin = player.Position + Vector3.up * .55f;
        Vector3 candidate = player.Position + forward * 1.15f + side;
        Vector3 delta = candidate + Vector3.up * .55f - origin;
        if (physics.Raycast(origin, delta.normalized, out RaycastHit obstacle, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            candidate = origin + delta.normalized * Mathf.Max(.2f, obstacle.distance - .3f);
        int count = physics.Raycast(candidate + Vector3.up * 2, Vector3.down, hits, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity; floor = player.Position;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider.transform.IsChildOf(player.transform) || hit.collider.GetComponentInParent<PickupItem>() != null ||
                hit.collider.GetComponentInParent<ThrowableMannequin>() != null || hit.normal.y < .7f || hit.point.y > player.Position.y + .5f || hit.distance >= closest) continue;
            closest = hit.distance; floor = hit.point;
        }
        return !float.IsPositiveInfinity(closest);
    }
}
