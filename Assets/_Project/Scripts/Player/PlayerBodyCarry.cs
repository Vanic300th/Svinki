using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(210)]
public sealed class PlayerBodyCarry : MonoBehaviour
{
    private static readonly List<PlayerBodyCarry> bodies = new List<PlayerBodyCarry>();
    private PlayerAvatar avatar;
    private NetworkPlayer network;
    private PlayerRagdoll ragdoll;
    private Transform pickup;
    private SphereCollider pickupShape;
    private CharacterController controller;
    private bool wasCarried;
    private ShoppingCart ignoredCart;
    public PlayerAvatar Holder { get; private set; }
    public ShoppingCart Cart { get; private set; }
    public bool IsDead => avatar != null && !avatar.IsAlive;
    public Vector3 Position => ragdoll != null ? ragdoll.PelvisPosition : transform.position;
    public bool Authority => network == null || network.IsServerInitialized;
    public static PlayerBodyCarry ForHolder(PlayerAvatar player) => player == null ? null : bodies.Find(b => b != null && b.Holder == player);
    private void Awake()
    {
        avatar = GetComponent<PlayerAvatar>(); network = GetComponent<NetworkPlayer>(); ragdoll = GetComponent<PlayerRagdoll>();
        controller = GetComponent<CharacterController>();
        var go = new GameObject("Body pickup area"); go.transform.SetParent(transform, false);
        pickup = go.transform; pickupShape = go.AddComponent<SphereCollider>(); pickupShape.radius = .65f; pickupShape.isTrigger = true; pickupShape.enabled = false;
    }
    private void OnEnable() { if (!bodies.Contains(this)) bodies.Add(this); }
    private void OnDisable() { bodies.Remove(this); }
    public bool Use(PlayerAvatar player, ShoppingCart cart = null)
    {
        if (!Authority || !IsDead || player == null || !player.IsAlive || player.gameObject.scene != gameObject.scene || NetworkLobby.Instance?.Results != null) return false;
        if (player.GetComponent<NetworkPlayer>() is NetworkPlayer actor && NetworkLobby.Instance?.CanEditOutfit(actor) != true) return false;
        if (Holder == player)
        {
            if (cart != null && (cart.gameObject.scene != gameObject.scene || cart.GetComponent<CartCargo>()?.CanAccess(player) != true)) return false;
            Attach(null, cart); return true;
        }
        if (Holder != null || cart != null || !CarryRules.FreeHands(player) || Vector3.Distance(player.EyePosition, Position) > 3.8f) return false;
        Vector3 direction = Position - player.EyePosition;
        if (direction.sqrMagnitude > .01f && gameObject.scene.GetPhysicsScene().Raycast(player.EyePosition, direction.normalized,
            out var blocker, direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !blocker.collider.transform.IsChildOf(transform)) return false;
        Attach(player, null); return true;
    }
    private void Attach(PlayerAvatar holder, ShoppingCart cart)
    {
        if (network != null) network.SetBodyAttachment(holder?.GetComponent<NetworkObject>(), cart?.GetComponent<NetworkObject>());
        else { Holder = holder; Cart = cart; }
    }
    public void Apply(NetworkObject holder, NetworkObject cart) { Holder = holder?.GetComponent<PlayerAvatar>(); Cart = cart?.GetComponent<ShoppingCart>(); }
    private void LateUpdate()
    {
        if (avatar == null) return;
        if (ignoredCart != Cart && Authority)
        {
            ragdoll?.SetCartCollision(ignoredCart, false);
            ignoredCart = Cart; ragdoll?.SetCartCollision(ignoredCart, true);
        }
        if (pickupShape != null) pickupShape.enabled = IsDead && Holder == null;
        if (!IsDead)
        {
            if (wasCarried) ragdoll?.ReleaseCarry(); wasCarried = false; return;
        }
        if (controller != null) controller.enabled = false;
        if (Authority && Holder != null && (!Holder.IsAlive || !Holder.isActiveAndEnabled || NetworkLobby.Instance?.Results != null)) Attach(null, null);
        bool carried = Holder != null || Cart != null;
        if (carried)
        {
            Vector3 point = Cart != null ? Cart.transform.TransformPoint(new Vector3(0, 1.1f, 0)) : Holder.EyePosition + Holder.EyeRotation() * new Vector3(0, -.35f, 1.1f);
            if (Cart == null)
            {
                Vector3 delta = point - Holder.EyePosition;
                if (gameObject.scene.GetPhysicsScene().Raycast(Holder.EyePosition, delta.normalized, out var obstruction, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) point = Holder.EyePosition + delta.normalized * Mathf.Max(.25f, obstruction.distance - .4f);
            }
            if (Authority) ragdoll?.CarryAt(point);
        }
        else if (wasCarried) ragdoll?.ReleaseCarry();
        wasCarried = carried;
        var position = Position;
        if (Authority || avatar.IsLocal) transform.position = position - Vector3.up * .3f;
        if (pickup != null) pickup.position = position;
    }
    public static void ReviveInLift(RoundFinishStation station)
    {
        if (station?.Elevator == null) return;
        foreach (var corpse in bodies.ToArray())
        {
            if (corpse == null || !corpse.Authority || !corpse.IsDead || corpse.Cart == null || corpse.Cart.gameObject.scene != station.gameObject.scene ||
                (corpse.Cart.GetComponent<CartCargo>()?.Items.Count ?? 0) <= 0 || !station.Elevator.Contains(corpse.Cart.transform.position)) continue;
            var point = corpse.Cart.transform.position + corpse.Cart.transform.right * 1.15f;
            point.y = station.Center.y + .1f;
            corpse.network?.Revive(point);
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => bodies.Clear();
}

public static class CarryRules
{
    public static bool FreeHands(PlayerAvatar player) => player != null && player.IsAlive && player.GetComponent<PlayerKnockdown>()?.IsDown != true &&
        ShoppingCart.For(player) == null && player.GetComponent<PlayerLobbyHands>()?.Held == null && player.GetComponent<PlayerMonkeyCarry>()?.Held == null &&
        player.GetComponent<PlayerMannequinCarry>()?.Held == null && player.GetComponent<PlayerPhotoCamera>()?.Equipped != true &&
        PlayerBodyCarry.ForHolder(player) == null && PlayerBallForm.ForHolder(player) == null && player.GetComponent<PlayerBallForm>()?.IsBall != true;
    public static Quaternion EyeRotation(this PlayerAvatar player) => player.TryGetView(out var view) ? view.Rotation : player.transform.rotation;
}
