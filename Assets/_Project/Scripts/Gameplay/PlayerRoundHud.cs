using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(1300)]
public sealed class PlayerRoundHud : MonoBehaviour
{
    private NetworkLobby lobby;
    private TMP_FontAsset font;
    private GameObject minimap, statusRoot, spectatorRoot;
    private StoreMap currentMap;
    private StoreMinimapGraphic graphic;
    private TMP_Text status, spectating, mapTitle, mapLegend;
    private UnityEngine.UI.Button restart;
    private readonly Dictionary<Object, TMP_Text> markers = new Dictionary<Object, TMP_Text>();
    public void Build(Transform canvas, TMP_FontAsset textFont, NetworkLobby session)
    {
        lobby = session; font = textFont;
        minimap = Rect("Minimap", canvas, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -22), new Vector2(340, 400)).gameObject;
        minimap.AddComponent<UnityEngine.UI.Image>().color = new Color(.025f, .045f, .06f, .94f);
        mapTitle = Text(Rect("Title", minimap.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -9), new Vector2(320, 30)), "STORE MAP · NORTH", 20);
        var viewport = Rect("Map viewport", minimap.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(315, 315));
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        graphic = Rect("Floorplan", viewport, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(315, 315)).gameObject.AddComponent<StoreMinimapGraphic>();
        graphic.raycastTarget = false;
        mapLegend = Text(Rect("Legend", minimap.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(320, 42)), "Players ▲ · Carts ■ · Lift L\nNames match player colours", 16);
        statusRoot = Rect("Round health", canvas, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -82), new Vector2(285, 49)).gameObject;
        statusRoot.AddComponent<UnityEngine.UI.Image>().color = new Color(.025f, .045f, .06f, .94f);
        status = Text(Rect("Hits remaining", statusRoot.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(260, 40)), "HITS LEFT  2 / 2", 23);
        spectatorRoot = Rect("Spectator banner", canvas, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 95), new Vector2(880, 95)).gameObject;
        spectatorRoot.AddComponent<UnityEngine.UI.Image>().color = new Color(.025f, .045f, .06f, .94f);
        spectating = Text(Rect("Spectator status", spectatorRoot.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(850, 85)), "", 24);
        var rect = Rect("Restart single player", canvas, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 36), new Vector2(300, 54));
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.18f, .30f, .36f);
        restart = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); restart.targetGraphic = image;
        Text(Rect("Label", rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(280, 48)), "NEXT ROUND", 24);
        restart.onClick.AddListener(() => lobby.RestartOfflineRound());
    }
    private void LateUpdate()
    {
        if (lobby == null || minimap == null) return;
        bool playing = lobby.Results == null && (lobby.Offline || lobby.Snapshot.phase == SessionPhase.Round || lobby.Snapshot.phase == SessionPhase.Lobby) && !lobby.MenuVisible;
        minimap.SetActive(playing); statusRoot.SetActive(playing); spectatorRoot.SetActive(playing && lobby.IsSpectator);
        restart.gameObject.SetActive(playing && lobby.Offline && lobby.IsEliminated);
        if (!playing) return;
        var local = PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal);
        var life = local != null ? local.GetComponent<PlayerKnockdown>() : null;
        bool safeRoom = local != null && local.gameObject.scene.name == NetworkLobby.FriendslopSceneName;
        GameLanguage.Set(status, safeRoom ? "SAFE ROOM" : lobby.IsEliminated ? "ELIMINATED" : life != null ? "HITS LEFT  " + life.HitsRemaining + " / 2" : "SPECTATOR");
        status.color = life != null && life.Hits == 1 ? new Color(1, .65f, .3f) : Color.white;
        var target = SpectatorCamera.CurrentTarget;
        GameLanguage.Set(spectating, lobby.Offline ? "ELIMINATED\nStart the next round to play again" :
            "Ghost flight · WASD — move · Space / Ctrl — up / down · Shift — faster");
        if (lobby.Offline && lobby.IsEliminated && !lobby.MenuVisible) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        var reference = target != null ? target.gameObject.scene : local != null ? local.gameObject.scene : UnityEngine.SceneManagement.SceneManager.GetSceneByName("SampleScene");
        if (!reference.IsValid() || !reference.isLoaded) return;
        if (currentMap == null || currentMap.gameObject.scene != reference)
        {
            currentMap = reference.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StoreMap>()).FirstOrDefault();
            graphic.Bind(currentMap);
            GameLanguage.Set(mapTitle, safeRoom ? "LOBBY MAP · NORTH" : "STORE MAP · NORTH");
            GameLanguage.Set(mapLegend, safeRoom ? "Players ▲ · Freight lift L\nEnter the lift together to start" : "Players ▲ · Carts ■ · Return lift L\nNames match player colours");
        }
        if (target != null) graphic.Follow(target.transform.position);
        else if (local != null) graphic.Follow(local.Position);
        foreach (var marker in markers.Values) if (marker != null) marker.gameObject.SetActive(false);
        foreach (var player in PlayerRegistry.Players.Where(p => p != null && p.IsAlive && p.gameObject.scene == reference))
        {
            Mark(player, player.Position, "▲", PlayerIdentityColor.For(player.GetComponent<NetworkPlayer>()), -player.transform.eulerAngles.y, 21);
        }
        foreach (var cart in ShoppingCart.All.Where(c => c != null && c.gameObject.scene == reference)) Mark(cart, cart.transform.position, "■", new Color(1, .82f, .22f), 0, 15);
        var ready = RoundFinishStation.ForScene(reference); if (ready != null && ready.Elevator == null) Mark(ready, ready.transform.position, "R", new Color(.4f, 1, .58f), 0, 18);
        var lift = FindObjectsByType<LobbyFreightElevator>().FirstOrDefault(l => l.gameObject.scene == reference);
        if (lift != null) Mark(lift, lift.transform.position, "L", new Color(.4f, 1, .58f), 0, 18);
        foreach (var key in markers.Keys.Where(k => k == null).ToArray()) { if (markers[key] != null) Destroy(markers[key].gameObject); markers.Remove(key); }
    }
    private void Mark(Object key, Vector3 position, string symbol, Color color, float angle, float size)
    {
        if (!markers.TryGetValue(key, out var label))
        {
            label = Text(Rect("Map marker", graphic.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(22, 22)), symbol, size);
            markers.Add(key, label);
        }
        var projected = graphic.Project(position);
        label.gameObject.SetActive(currentMap != null && graphic.rectTransform.rect.Contains(projected));
        label.color = color; label.rectTransform.anchoredPosition = projected;
        label.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private TMP_Text Text(RectTransform rect, string text, float size)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size; GameLanguage.Set(label, text);
        label.color = Color.white; label.raycastTarget = false; label.richText = false; label.alignment = TextAlignmentOptions.Center; return label;
    }
}
