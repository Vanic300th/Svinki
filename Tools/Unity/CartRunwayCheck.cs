using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class CartRunwayCheck
{
    const string StateKey = "Svinki.CartRunway.Check";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Step(string message) { SessionState.SetString(StateKey, message); Debug.Log("CartRunway: " + message); }
    static void Warp(PlayerAvatar player, Vector3 position)
    {
        var cc = player.GetComponent<CharacterController>(); bool enabled = cc.enabled; cc.enabled = false;
        player.transform.position = position; cc.enabled = enabled; Physics.SyncTransforms();
    }
    static void Aim(PlayerAvatar player, Vector3 point)
    {
        var view = player.EyeCamera.GetComponent<GrayboxFirstPersonCamera>();
        Vector3 direction = point - (player.Position + Vector3.up * 1.65f);
        Vector3 angles = Quaternion.LookRotation(direction).eulerAngles;
        typeof(GrayboxFirstPersonCamera).GetField("yaw", Flags).SetValue(view, angles.y);
        typeof(GrayboxFirstPersonCamera).GetField("pitch", Flags).SetValue(view, Mathf.DeltaAngle(0, angles.x));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object current; bool running;
            try { running = routine.MoveNext(); current = running ? routine.Current : null; }
            catch (Exception e) { Step("FAILED: " + e); Debug.LogException(e); yield break; }
            if (!running) yield break;
            yield return current;
        }
    }
    public static string Offline()
    {
        Check(EditorApplication.isPlaying, "Requires Play Mode in Lobby");
        Check(!NetworkLobby.Instance.InSession, "Requires an isolated menu");
        Step("offline: loading"); NetworkLobby.Instance.StartCoroutine(Guard(OfflineTest())); return "Offline check started";
    }
    static IEnumerator OfflineTest()
    {
        var lobby = NetworkLobby.Instance; lobby.StartOffline();
        float deadline = Time.realtimeSinceStartup + 30;
        while ((!lobby.Offline || lobby.Busy || PlayerRegistry.Players.Count == 0) && Time.realtimeSinceStartup < deadline) yield return null;
        Check(lobby.Offline && !lobby.Busy, "Offline load failed");
        var player = PlayerRegistry.Players.Single(p => p.IsLocal);
        foreach (var root in player.gameObject.scene.GetRootGameObjects())
            foreach (var brain in root.GetComponentsInChildren<ThiefBrain>()) brain.enabled = false;
        var layout = UnityEngine.Object.FindAnyObjectByType<RoundClothingLayout>();
        Check(layout.Applied && layout.Count == 48, "Offline layout not applied");
        var locations = ClothingPickup.All.Select(c => c.transform.position).ToArray();
        layout.Shuffle(123); var shuffled = ClothingPickup.All.Select(c => c.transform.position).ToArray();
        Check(shuffled.Distinct().Count() == locations.Length && shuffled.All(p => locations.Contains(p)), "Shuffle changed anchors or produced duplicates");
        Check(shuffled.Where((p, i) => p != locations[i]).Count() > 10, "Clothing did not move");
        layout.Shuffle(456); Check(ClothingPickup.All.Where((c, i) => c.transform.position != shuffled[i]).Count() > 10, "Second layout did not differ");
        for (int seed = 0; seed < 100; seed++)
        { var permutation = RoundClothingLayout.Permutation(48, seed); Check(permutation.Distinct().Count() == 48 && permutation.All(i => i >= 0 && i < 48), "Invalid permutation"); }
        Check(ShoppingCart.All.Count == 4 && ShoppingCart.All.All(c => c.HasAuthority), "Offline cart authority missing");
        var cart = ShoppingCart.All.First();
        Warp(player, cart.transform.position + cart.transform.forward * -2.4f); Aim(player, cart.transform.position + Vector3.up * .8f);
        yield return null; yield return null;
        Check(cart.TryUse(player, false) && cart.Driver == player, "Cannot grab cart");
        Check(!player.GetComponent<CharacterController>().enabled, "Driver movement not constrained");
        Vector3 before = cart.transform.position;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W, Key.LeftShift));
        yield return new WaitForSecondsRealtime(.45f);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        Check(Vector3.Distance(before, cart.transform.position) > .6f && cart.Speed > 4, "Cart does not accelerate");
        cart.Release(player, true); Check(cart.Driver == null && player.GetComponent<CharacterController>().enabled && cart.Speed >= 8, "Launch/exit failed");
        yield return new WaitForSecondsRealtime(.35f);
        // Park and test a seated pig, driver exclusivity, impact and re-entry.
        var body = cart.GetComponent<Rigidbody>(); body.linearVelocity = Vector3.zero; body.position = before;
        Warp(player, before + Vector3.right * 1.5f); yield return new WaitForFixedUpdate();
        Check(cart.TryUse(player, true), "Cannot sit in basket"); yield return null; yield return null;
        Check(cart.Rider == player && player.CrouchAmount > .9f && Vector3.Distance(player.Position, cart.transform.position) < 1, "Rider not seated/squashed");
        cart.Release(player, false); Check(cart.Rider == null && player.GetComponent<CharacterController>().enabled, "Rider cannot leave");
        Warp(player, before + Vector3.back * 2); Check(cart.TryUse(player, false), "Cannot use cart again");
        cart.Release(player, false);
        Step("offline: carts and layout passed; podium");
        var rules = OutfitRatingCatalog.Load(); player.Outfit.SetItems(rules.references[0].items);
        var station = RoundFinishStation.ForScene(player.gameObject.scene); Warp(player, station.Center);
        lobby.EndRound(); yield return null; yield return null;
        Check(lobby.Results != null && !lobby.InputAllowed, "Final results missing");
        var runway = lobby.GetComponent<FinalRunwayView>();
        Check(runway != null && runway.Showing && runway.PresentedIndex == 0, "Final runway did not open");
        Check(lobby.transform.Find("Final Runway Studio/Runway Pig") != null, "No dressed pig on runway");
        var pig = lobby.transform.Find("Final Runway Studio/Runway Pig");
        Check(pig.GetComponentsInChildren<SkinnedMeshRenderer>().Length >= 5, "Outfit not fitted on runway");
        foreach (var skin in pig.GetComponentsInChildren<SkinnedMeshRenderer>()) Check(skin.bones.All(b => b != null), "Broken runway garment rig");
        lobby.RunwayPose(2); Check(lobby.Results.entries[0].presentationPose == 2, "Offline pose missing");
        yield return new WaitForSecondsRealtime(3);
        yield return new WaitForEndOfFrame(); Capture("podium-offline.png");
        Check(runway.transform.Find("Session Canvas/Outfit Results/Final Runway").GetComponentsInChildren<TMP_Text>().All(t => !t.isTextOverflowing), "Runway text overflow");
        yield return new WaitForSecondsRealtime(5.2f);
        Check(!runway.Showing, "Runway did not finish automatically");
        int count = player.Outfit.Items.Count(); lobby.ContinueAfterResults(); yield return null;
        Check(lobby.InputAllowed && lobby.Results == null && player.Outfit.Items.Count() == count, "Continue lost clothing or input");
        lobby.Leave(); deadline = Time.realtimeSinceStartup + 20;
        while (lobby.Offline && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!lobby.Offline, "Offline unload failed");
        Step("PASSED offline: 100 permutations, 48 preserved anchors, cart push/sprint/launch/seat/exit/re-entry, dressed runway/pose/automatic finish/continue");
    }
    static void Capture(string name)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes("ArtSource/CartRunway/" + name, image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
    }
    public static string PushHost()
    { NetworkLobby.Instance.StartCoroutine(DriveHost()); return "Driving through owner input RPC"; }
    static IEnumerator DriveHost()
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W, Key.LeftShift));
        yield return new WaitForSecondsRealtime(.35f);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
    }
    public static string Snapshot()
    {
        var lobby = NetworkLobby.Instance;
        var data = new { phase = lobby.Snapshot.phase.ToString(), count = lobby.Snapshot.players.Length,
            carts = ShoppingCart.All.Select(c => new { name=c.name, position=new { x=c.transform.position.x,y=c.transform.position.y,z=c.transform.position.z }, speed=c.Speed,
                driver=c.Driver?.GetComponent<NetworkPlayer>()?.ParticipantName, rider=c.Rider?.GetComponent<NetworkPlayer>()?.ParticipantName }),
            pickups = ClothingPickup.All.Select(p => new { name=p.name, position=new { x=p.transform.position.x,y=p.transform.position.y,z=p.transform.position.z }, rotation=new { x=p.transform.rotation.x,y=p.transform.rotation.y,z=p.transform.rotation.z,w=p.transform.rotation.w } }), status=SessionState.GetString(StateKey, "") };
        return Newtonsoft.Json.JsonConvert.SerializeObject(data);
    }
}
