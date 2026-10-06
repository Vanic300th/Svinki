using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Destructive flow check: run only in an isolated host session with a saved SessionEditBackup.
public static class ReadyInventoryCheck
{
    const string StateKey = "Svinki.ReadyInventory.Check";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static NetworkObject extra;
    static IDictionary members;
    static NetworkLobby lobby;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Step(string value) => SessionState.SetString(StateKey, value);
    static object Field(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
    static void Call(string name) => typeof(NetworkLobby).GetMethod(name, Flags).Invoke(lobby, null);
    static void Warp(PlayerAvatar player, Vector3 position)
    {
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.position = position;
        if (controller != null) controller.enabled = true;
        Physics.SyncTransforms();
    }
    static void Cleanup()
    {
        if (members != null) members.Remove("ready-test");
        if (extra != null && lobby != null) lobby.GetComponent<NetworkManager>().ServerManager.Despawn(extra);
        extra = null;
    }
    public static string Main()
    {
        lobby = NetworkLobby.Instance;
        Check(lobby.IsHost && lobby.Snapshot.phase == SessionPhase.Round && lobby.Snapshot.players.Length == 1,
            "Requires isolated single-player host.");
        Check(SessionState.GetString("Svinki.EditBackup", "").Length > 0, "Back up the current game before running.");
        lobby.StartCoroutine(Guard(Test())); return "Testing inventory, drops and two server participants' readiness.";
    }
    static IEnumerator Guard(IEnumerator test)
    {
        while (true)
        {
            object current; bool running;
            try { running = test.MoveNext(); current = running ? test.Current : null; }
            catch (Exception e) { Cleanup(); Step("FAILED: " + e); Debug.LogException(e); yield break; }
            if (!running) yield break;
            yield return current;
        }
    }
    public static string PhysicalButton()
    {
        lobby = NetworkLobby.Instance;
        Check(lobby.IsHost && lobby.Snapshot.phase == SessionPhase.Round && lobby.Snapshot.players.Length == 1, "Requires isolated host.");
        lobby.StartCoroutine(Guard(PressPhysicalButton())); return "Checking aim, readiness prompt and E on the physical button.";
    }
    static IEnumerator PressPhysicalButton()
    {
        var player = PlayerRegistry.Players.Single(p => p.IsLocal);
        var station = RoundFinishStation.ForScene(player.gameObject.scene);
        var view = player.EyeCamera.GetComponent<GrayboxFirstPersonCamera>();
        float yaw = (float)Field(view, "yaw"), pitch = (float)Field(view, "pitch");
        Warp(player, station.Center + Vector3.up * .05f);
        Set(view, "yaw", 0f);
        var direction = station.transform.position + new Vector3(0, .15f, -.15f) - (player.Position + Vector3.up * 1.65f + Vector3.forward * .22f);
        Set(view, "pitch", Mathf.DeltaAngle(0, Quaternion.LookRotation(direction).eulerAngles.x));
        yield return null; yield return null;
        var interactor = player.EyeCamera.GetComponent<PlayerPickupInteractor>();
        Check((RoundFinishStation)Field(interactor, "finishTarget") == station, "Raycast did not target physical finish button.");
        var hint = lobby.transform.Find("Session Canvas/Finish readiness");
        Check(hint.gameObject.activeSelf, "Gathering prompt missing.");
        var label = station.GetComponentInChildren<TextMeshPro>();
        label.ForceMeshUpdate(); Check(!label.isTextOverflowing, "Physical button label overflow.");
        yield return new WaitForEndOfFrame();
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes("ArtSource/ReadyInventory/ready-button.png", image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.E)); yield return null; yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); yield return new WaitForSecondsRealtime(.4f);
        Check(lobby.Results != null, "E did not finish the ready single-player round.");
        Set(view, "yaw", yaw); Set(view, "pitch", pitch);
        lobby.Leave(); float deadline = Time.realtimeSinceStartup + 20;
        while (lobby.InSession && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!lobby.InSession, "Physical button check did not leave.");
        SessionState.SetString("Svinki.PhysicalReady.Check", "passed: live camera raycast, start HUD and E to results");
    }
    static IEnumerator Test()
    {
        var player = PlayerRegistry.Players.Single(p => p.IsLocal);
        var network = player.GetComponent<NetworkPlayer>();
        var station = RoundFinishStation.ForScene(player.gameObject.scene);
        Check(station != null, "Finish station missing.");
        var manager = lobby.GetComponent<NetworkManager>();
        var rules = OutfitRatingCatalog.Load();
        var clothing = rules.references[0].items;
        members = (IDictionary)Field(lobby, "members");
        object real = members[lobby.Identity];
        object second = Activator.CreateInstance(real.GetType(), true);
        Set(second, "Id", "ready-test"); Set(second, "Name", "Проверка готовности");
        Set(second, "Connection", Field(real, "Connection"));
        var prefab = (NetworkObject)Field(lobby, "playerPrefab");
        extra = UnityEngine.Object.Instantiate(prefab, station.Center + new Vector3(1.5f, .05f, 0), Quaternion.identity);
        extra.GetComponent<NetworkPlayer>().SetIdentity("ready-test", "Проверка готовности");
        manager.ServerManager.Spawn(extra, null, player.gameObject.scene);
        Set(second, "Avatar", extra); members.Add("ready-test", second); Call("Publish");
        yield return new WaitForSecondsRealtime(.4f);
        Step("out-of-zone and consensus readiness");
        Warp(player, station.Center + Vector3.back * 7); lobby.FinishReady(true);
        yield return new WaitForSecondsRealtime(.3f);
        Check(!lobby.IsFinishReady && lobby.Snapshot.phase == SessionPhase.Round, "Out-of-zone readiness accepted.");
        Warp(player, station.Center + Vector3.up * .05f); station.Press();
        yield return new WaitForSecondsRealtime(.3f);
        Check(lobby.IsFinishReady && lobby.Snapshot.phase == SessionPhase.Round, "One participant ended a two-person round.");
        Warp(player, station.Center + Vector3.back * 7);
        yield return new WaitForSecondsRealtime(.35f);
        Check(!lobby.IsFinishReady, "Walking away did not cancel readiness.");
        Warp(player, station.Center + Vector3.up * .05f); station.Press();
        yield return new WaitForSecondsRealtime(.3f);
        player.Outfit.SetItems(Array.Empty<ClothingDefinition>()); player.Outfit.SetItems(clothing);
        yield return new WaitForSecondsRealtime(.2f);
        Check(!lobby.IsFinishReady, "Outfit change did not cancel readiness.");
        var body = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "Body");
        var fullBody = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Pigs/PigAvatar.prefab")
            .GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "Body").sharedMesh;
        Check(body.sharedMesh != fullBody, "Skin beneath trousers was not hidden.");
        var sourcePickups = clothing.Select(c => ClothingPickup.All.First(p => p.Clothing == c && p.gameObject.scene == player.gameObject.scene)).ToArray();
        foreach (var pickup in sourcePickups) pickup.Item.SetAvailable(false);
        var view = lobby.GetComponent<OutfitInventoryView>();
        Step("keyboard and inventory UI");
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Tab)); yield return null; yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); yield return null;
        Check(lobby.InventoryOpen && !lobby.InputAllowed && Cursor.lockState == CursorLockMode.None, "Tab did not open/lock inventory.");
        var panel = lobby.transform.Find("Session Canvas/Outfit Inventory");
        Check(panel.gameObject.activeSelf, "Inventory not visible.");
        Check(UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e => e.isActiveAndEnabled) == 1, "Multiple active EventSystems.");
        yield return new WaitForEndOfFrame();
        Directory.CreateDirectory("ArtSource/ReadyInventory");
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes("ArtSource/ReadyInventory/inventory.png", image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
        Check(panel.GetComponentsInChildren<TMP_Text>().All(t => !t.isTextOverflowing), "Inventory text overflow.");
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape)); yield return null; yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); yield return null;
        Check(!lobby.InventoryOpen && !lobby.MenuVisible, "Esc opened pause menu instead of closing inventory.");
        view.SetOpen(true); yield return null;
        Step("dropping all four slots through the UI");
        panel.Find("Выбросить всё").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(.4f);
        Check(player.Outfit.IsEmpty && body.sharedMesh == fullBody, "Drop-all did not restore naked pig.");
        Check(sourcePickups.All(p => p.IsOnFloor && Vector3.Distance(p.transform.position, player.Position) < 2.5f), "Dropped clothing missing nearby.");
        Check(rules.Evaluate(player.Outfit.Items).score == 0, "Naked pig did not score zero.");
        foreach (var pickup in sourcePickups) pickup.GetComponent<NetworkPickup>().TryCollect(network);
        yield return new WaitForSecondsRealtime(.3f);
        Check(player.Outfit.Count == 4 && sourcePickups.All(p => !p.IsOnFloor), "Recollecting drops failed.");
        foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.name == "Выбросить"))
        { button.onClick.Invoke(); yield return new WaitForSecondsRealtime(.12f); }
        Check(player.Outfit.IsEmpty, "Individual drop buttons failed.");
        var before = ClothingPickup.All.Count;
        player.Outfit.Add(clothing[0]); network.RequestDropClothing(clothing[0].Slot);
        yield return new WaitForSecondsRealtime(.35f);
        var clone = ClothingPickup.All.FirstOrDefault(p => p.name.EndsWith("(Clone)") && p.Clothing == clothing[0] && p.gameObject.scene == player.gameObject.scene);
        Check(player.Outfit.IsEmpty && ClothingPickup.All.Count == before + 1 && clone != null && clone.IsOnFloor && clone.GetComponent<NetworkObject>().IsServerInitialized,
            "Fallback network pickup spawning failed.");
        network.RequestDropClothing(clothing[0].Slot); yield return new WaitForSecondsRealtime(.2f);
        Check(ClothingPickup.All.Count == before + 1, "Repeated drop duplicated an empty slot.");
        clone.GetComponent<NetworkPickup>().TryCollect(extra.GetComponent<NetworkPlayer>());
        Check(extra.GetComponent<PlayerOutfit>().Get(clothing[0].Slot) == clothing[0] && !clone.IsOnFloor, "Another player could not collect drop.");
        manager.ServerManager.Despawn(clone.GetComponent<NetworkObject>());
        view.SetOpen(false);
        Step("all participants ready and results");
        Set(second, "FinishReady", true); Call("Publish"); station.Press();
        yield return new WaitForSecondsRealtime(.4f);
        Check(lobby.Results != null && lobby.Results.entries.Length == 2 && lobby.Snapshot.phase == SessionPhase.Results,
            "All ready did not present both inventories.");
        Check(lobby.Results.entries.Single(e => e.id == lobby.Identity).rating.score == 0, "Empty inventory result wrong.");
        Cleanup(); lobby.ContinueAfterResults();
        float deadline = Time.realtimeSinceStartup + 20;
        while (lobby.Snapshot.phase != SessionPhase.Lobby && Time.realtimeSinceStartup < deadline) yield return null;
        Check(lobby.Snapshot.phase == SessionPhase.Lobby, "Results continue failed.");
        lobby.Leave(); deadline = Time.realtimeSinceStartup + 20;
        while (lobby.InSession && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!lobby.InSession, "Test host failed to leave.");
        Step("offline drop and finish"); lobby.StartOffline(); deadline = Time.realtimeSinceStartup + 20;
        while ((!lobby.Offline || !PlayerRegistry.Players.Any(p => p.IsLocal)) && Time.realtimeSinceStartup < deadline) yield return null;
        player = PlayerRegistry.Players.FirstOrDefault(p => p.IsLocal);
        Check(player != null && lobby.Offline, "Offline spawn missing.");
        yield return null; yield return null;
        station = RoundFinishStation.ForScene(player.gameObject.scene); Warp(player, station.Center + Vector3.up * .05f);
        player.Outfit.SetItems(clothing); view.SetOpen(true); yield return null;
        panel.Find("Выбросить всё").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
        Check(player.Outfit.IsEmpty, "Offline drop-all failed.");
        view.SetOpen(false); station.Press(); yield return null;
        Check(lobby.Results != null && lobby.Results.entries[0].rating.score == 0, "Offline finish failed.");
        lobby.Leave(); deadline = Time.realtimeSinceStartup + 20;
        while (lobby.Offline && Time.realtimeSinceStartup < deadline) yield return null;
        Step("passed: Tab/Esc, UI bounds, naked restoration, individual/all drops, reuse and fallback spawn, recollect by another server player, readiness consensus/cancellation, results, offline");
    }
}
