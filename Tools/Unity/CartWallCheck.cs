using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Runtime-only course: exercise actual PhysX contacts without changing scene assets.
public sealed class CartWallProbe : MonoBehaviour
{
    public ShoppingCart Cart;
    public int Contacts;
    private void OnCollisionEnter(Collision collision)
    { if (collision.gameObject.GetComponent<ShoppingCart>() == Cart) Contacts++; }
}

public static class CartWallCheck
{
    public static string Start()
    {
        var player = PlayerRegistry.Players.First(p => p.IsLocal);
        if (ShoppingCart.For(player) != null || !player.GetComponent<PlayerKnockdown>().CanFall)
            throw new Exception("Start alive and outside a cart");
        SessionState.SetString("Svinki.CartWall.Check", "RUNNING");
        player.StartCoroutine(Run(player));
        return "Testing six real wall impacts: driver and basket passenger";
    }
    private static IEnumerator Run(PlayerAvatar player)
    {
        var cart = ShoppingCart.All.First(c => c.HasAuthority);
        var body = cart.GetComponent<Rigidbody>();
        var life = player.GetComponent<PlayerKnockdown>();
        var controller = player.GetComponent<CharacterController>();
        var movement = player.GetComponent<GrayboxPlayerController>();
        var pickup = UnityEngine.Object.FindAnyObjectByType<PlayerPickupInteractor>();
        Vector3 oldPlayer = player.Position, oldCart = body.position, oldVelocity = body.linearVelocity;
        Quaternion oldRotation = body.rotation;
        bool moved = movement.enabled, interacted = pickup != null && pickup.enabled;
        uint oldFall = life.FallId;
        int oldHits = life.Hits;
        GameObject floor = null, wall = null;
        bool passed = false;
        try
        {
            movement.enabled = false;
            if (pickup != null) pickup.enabled = false;
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Temporary cart collision floor";
            floor.transform.position = new Vector3(100, 29.5f, 0);
            floor.transform.localScale = new Vector3(12, 1, 12);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Temporary cart collision wall";
            wall.transform.position = new Vector3(100, 31.5f, 3);
            wall.transform.localScale = new Vector3(8, 3, .5f);
            var probe = wall.AddComponent<CartWallProbe>(); probe.Cart = cart;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                bool seated = attempt >= 3;
                body.position = new Vector3(100, 30.05f, 0); body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
                if (ShoppingCart.For(player) == null)
                {
                    controller.enabled = false; player.transform.position = body.position + Vector3.back * 2;
                    controller.enabled = true;
                    if (!cart.TryUse(player, seated)) throw new Exception("Cart use rejected: distance=" + Vector3.Distance(player.Position, cart.transform.position) + ", speed=" + cart.Speed);
                }
                int before = probe.Contacts;
                body.linearVelocity = Vector3.forward * 8;
                for (int frame = 0; frame < 65; frame++)
                {
                    if (!seated) cart.SubmitInput(player, Vector2.up, true);
                    yield return new WaitForFixedUpdate();
                    if (life.IsDown || life.FallId != oldFall || life.Hits != oldHits ||
                        (seated ? cart.Rider : cart.Driver) != player)
                        throw new Exception("Wall impact knocked down or ejected an occupant");
                }
                if (probe.Contacts <= before || body.position.z > 2.75f)
                    throw new Exception("No real wall contact, or cart passed through wall");
                if (attempt == 2) cart.Release(player, false);
            }
            passed = true;
            string result = "PASS: six actual 8 m/s wall contacts; driver and passenger stay attached and upright; no fall/hit change; wall blocks cart";
            Directory.CreateDirectory("ArtSource/ExpandedRound");
            File.WriteAllText("ArtSource/ExpandedRound/cart-wall-validation.txt", result);
            SessionState.SetString("Svinki.CartWall.Check", result);
        }
        finally
        {
            if (!passed) SessionState.SetString("Svinki.CartWall.Check", "FAILED: inspect runtime log");
            cart.Release(player, false);
            body.position = oldCart; body.rotation = oldRotation; body.linearVelocity = oldVelocity;
            body.angularVelocity = Vector3.zero;
            controller.enabled = false; player.transform.position = oldPlayer; controller.enabled = true;
            movement.enabled = moved; if (pickup != null) pickup.enabled = interacted;
            if (wall != null) UnityEngine.Object.Destroy(wall);
            if (floor != null) UnityEngine.Object.Destroy(floor);
        }
    }
}
