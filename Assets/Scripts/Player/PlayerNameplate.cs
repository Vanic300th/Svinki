using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Screen overlay deliberately remains visible through geometry.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(1200)]
public sealed class PlayerNameplate : MonoBehaviour
{
    private static readonly List<Rect> occupied = new List<Rect>();
    private static int layoutFrame = -1;
    private NetworkPlayer player;
    private CharacterController body;
    private PlayerKnockdown knockdown;
    private Renderer[] avatarParts;
    private Canvas canvas;
    private TextMeshProUGUI label;
    private void Awake()
    {
        player = GetComponent<NetworkPlayer>(); body = GetComponent<CharacterController>(); knockdown = GetComponent<PlayerKnockdown>();
        var pig = GetComponentInChildren<PigAppearance>(true);
        avatarParts = pig != null ? pig.ModelRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
    }
    private void LateUpdate()
    {
        if (player == null || !player.IsClientStarted || PlayerChat.Instance == null) return;
        if (canvas == null)
        {
            var obj = new GameObject("Nickname overlay", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(obj, gameObject.scene);
            canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            var text = new GameObject("Nickname", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(obj.transform, false);
            label = text.GetComponent<TextMeshProUGUI>(); label.font = PlayerChat.Instance.Font;
            label.fontSize = 18; label.alignment = TextAlignmentOptions.Center; label.richText = false; label.raycastTarget = false;
            label.outlineColor = new Color(0, 0, 0, 1); label.outlineWidth = .25f;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.zero;
            label.rectTransform.sizeDelta = new Vector2(240, 52);
        }
        Camera camera = Camera.main;
        var lobby = NetworkLobby.Instance;
        float distance = camera != null ? Vector3.Distance(camera.transform.position, transform.position) : 999;
        canvas.enabled = !player.IsOwner && !player.IsDead && SpectatorCamera.CurrentTarget != player && camera != null && distance < 180 &&
            lobby?.Results == null && lobby?.MenuVisible != true;
        if (!canvas.enabled) return;
        Vector3 anchor = knockdown != null ? knockdown.HeadPosition : transform.position;
        float capsuleTop = body != null ? body.center.y + body.height * .5f : 1.8f;
        float top = knockdown != null && knockdown.VisualAmount > .1f ? anchor.y : transform.TransformPoint(new Vector3(0, capsuleTop, 0)).y;
        foreach (Renderer part in avatarParts)
            if (part != null && part.enabled && part.gameObject.activeInHierarchy) top = Mathf.Max(top, part.bounds.max.y);
        anchor.y = top + .25f;
        Vector3 screen = camera.WorldToScreenPoint(anchor);
        if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height)
        { canvas.enabled = false; return; }
        if (layoutFrame != Time.frameCount) { occupied.Clear(); layoutFrame = Time.frameCount; }
        Rect box = new Rect(screen.x - 120, screen.y, 240, 52);
        for (int attempt = 0; attempt < 6; attempt++)
        {
            bool overlap = false; foreach (Rect other in occupied) if (box.Overlaps(other)) { overlap = true; break; }
            if (!overlap) break;
            box.y += 40;
        }
        box.y = Mathf.Min(box.y, Screen.height - 54); occupied.Add(box);
        label.rectTransform.anchoredPosition = new Vector2(screen.x, box.y + 26);
        label.color = new Color(.83f, .96f, 1, Mathf.Lerp(1, .65f, Mathf.Clamp01(distance / 180)));
        label.text = (string.IsNullOrWhiteSpace(player.ParticipantName) ? "Pig" : player.ParticipantName) + "\n" + Mathf.RoundToInt(distance) + " m";
    }
    private void OnDisable() { if (canvas != null) canvas.enabled = false; }
    private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { occupied.Clear(); layoutFrame = -1; }
}
