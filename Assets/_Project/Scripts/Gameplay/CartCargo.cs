using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CargoAction : byte { StoreWorn, LoadPickup, Equip, Unload }

/// <summary>Eight independent garments. Only the host mutates cargo and inventories.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(ShoppingCart))]
public sealed class CartCargo : MonoBehaviour
{
    public const int Capacity = 8;
    [SerializeField] private ClothingDefinition[] catalog = Array.Empty<ClothingDefinition>();
    private readonly List<ClothingDefinition> items = new List<ClothingDefinition>();
    private readonly List<GameObject> visuals = new List<GameObject>();
    private ShoppingCart cart;
    private NetworkShoppingCart network;
    public IReadOnlyList<ClothingDefinition> Items => items;
    public int Revision { get; private set; }
    private void Awake() { cart = GetComponent<ShoppingCart>(); network = GetComponent<NetworkShoppingCart>(); }
    public bool CanAccess(PlayerAvatar player)
    {
        if (player == null || !player.IsAlive || player.gameObject.scene != gameObject.scene ||
            player.GetComponent<PlayerKnockdown>()?.IsDown == true || NetworkLobby.Instance?.Results != null || cart.Speed > 3) return false;
        Vector3 eye = player.EyePosition;
        Vector3 point = GetComponent<BoxCollider>().ClosestPoint(eye);
        Vector3 direction = point - eye;
        return direction.magnitude <= 3.8f && (direction.magnitude < .05f ||
            gameObject.scene.GetPhysicsScene().Raycast(eye, direction.normalized, out RaycastHit hit,
                direction.magnitude + .1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(transform));
    }
    public bool Transfer(PlayerAvatar player, CargoAction action, int index, int revision, ClothingPickup pickup = null)
    {
        if (!cart.HasAuthority || revision != Revision || !CanAccess(player)) return false;
        var session = NetworkLobby.Instance;
        var onlinePlayer = player.GetComponent<NetworkPlayer>();
        if (onlinePlayer != null && session?.CanEditOutfit(onlinePlayer) != true) return false;
        switch (action)
        {
            case CargoAction.StoreWorn:
                if (index < 0 || index > 3 || items.Count >= Capacity) return false;
                var clothing = player.Outfit.Get((ClothingSlot)index);
                if (clothing == null || !player.Outfit.Remove((ClothingSlot)index, "Stored in cart", out _)) return false;
                items.Add(clothing);
                break;
            case CargoAction.LoadPickup:
                if (items.Count >= Capacity || pickup == null || !pickup.IsOnFloor || pickup.Clothing == null ||
                    pickup.gameObject.scene != gameObject.scene || Vector3.Distance(pickup.transform.position, transform.position) > 4 ||
                    Vector3.Distance(pickup.transform.position, player.Position) > 4) return false;
                Vector3 delta = pickup.transform.position - player.EyePosition;
                if (delta.magnitude > .01f && gameObject.scene.GetPhysicsScene().Raycast(player.EyePosition, delta.normalized,
                    out RaycastHit blocker, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    !blocker.collider.transform.IsChildOf(pickup.transform) && !blocker.collider.transform.IsChildOf(transform)) return false;
                pickup.TakeForCargo();
                items.Add(pickup.Clothing);
                break;
            case CargoAction.Equip:
                if (index < 0 || index >= items.Count || !player.Outfit.TryAdd(items[index])) return false;
                items.RemoveAt(index);
                break;
            case CargoAction.Unload:
                if (index < 0 || index >= items.Count) return false;
                // Spawn exactly one pickup before removing this entry. Never reuse an item owned by another player.
                if (!ClothingDropper.TryPlace(items[index], player, onlinePlayer != null ? onlinePlayer.NetworkObject.NetworkManager : null)) return false;
                items.RemoveAt(index);
                break;
            default: return false;
        }
        Revision++;
        RebuildVisuals(); network?.SetCargo(items.Select(c => c.name).ToArray(), Revision);
        return true;
    }
    public void Apply(string[] ids, int revision)
    {
        items.Clear();
        foreach (string id in ids ?? Array.Empty<string>())
        {
            var definition = catalog.FirstOrDefault(c => c != null && c.name == id);
            if (definition != null) items.Add(definition);
        }
        Revision = revision; RebuildVisuals();
    }
    private void RebuildVisuals()
    {
        foreach (var visual in visuals) if (visual != null) Destroy(visual);
        visuals.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            var clothing = items[i]; if (clothing.Model == null) continue;
            var holder = new GameObject("Cargo " + (i + 1)); holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(i % 2 == 0 ? -.19f : .19f, .68f + i / 4 * .16f, -.33f + (i / 2 % 2) * .62f);
            var fabric = Instantiate(clothing.Model, holder.transform);
            ClothingVisuals.Prepare(fabric, clothing.FabricMaterial, gameObject.layer);
            foreach (Animator animation in fabric.GetComponentsInChildren<Animator>(true)) animation.enabled = false;
            foreach (var behaviour in fabric.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            var renderers = fabric.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                float scale = .32f / Mathf.Max(.01f, bounds.size.x, bounds.size.y, bounds.size.z);
                fabric.transform.localScale *= scale;
                fabric.transform.position += holder.transform.position - (fabric.transform.position + (bounds.center - fabric.transform.position) * scale);
            }
            visuals.Add(holder);
        }
    }
}
