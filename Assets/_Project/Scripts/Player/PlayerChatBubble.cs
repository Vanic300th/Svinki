using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerChatBubble : MonoBehaviour
{
    private RectTransform bubble;
    private Canvas canvas;
    private TextMeshProUGUI label;
    private CharacterController body;
    private PlayerKnockdown knockdown;
    private float visibleUntil;

    public static void Show(GameObject player, string message)
    {
        if (player == null || PlayerChat.Instance == null) return;
        var avatar=player.GetComponent<PlayerAvatar>();
        string nickname=player.GetComponent<NetworkPlayer>()?.ParticipantName ?? NetworkLobby.Instance?.Nickname ?? "Player";
        PlayerChat.Instance.GetComponent<PlayerChatHistory>()?.Append(nickname,message,avatar!=null&&avatar.IsLocal);
        var chat = player.GetComponent<PlayerChatBubble>() ?? player.AddComponent<PlayerChatBubble>();
        chat.ShowMessage(message);
    }

    private void ShowMessage(string message)
    {
        if (bubble == null)
        {
            body = GetComponent<CharacterController>();
            knockdown = GetComponent<PlayerKnockdown>();
            var obj = new GameObject("Chat Bubble", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.Image));
            obj.transform.SetParent(transform, false);
            bubble = obj.GetComponent<RectTransform>();
            bubble.pivot = new Vector2(.5f, 0);
            bubble.localScale = Vector3.one * .005f;
            canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var background = obj.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(.025f, .035f, .055f, .9f); background.raycastTarget = false;
            var text = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(bubble, false);
            label = text.GetComponent<TextMeshProUGUI>();
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(18, 12); label.rectTransform.offsetMax = new Vector2(-18, -12);
            label.font = PlayerChat.Instance.Font; label.fontSize = 30;
            label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
            label.richText = false; label.raycastTarget = false;
        }
        label.text = message;
        Vector2 preferred = label.GetPreferredValues(message, 444, 0);
        bubble.sizeDelta = new Vector2(480, Mathf.Max(68, preferred.y + 24));
        visibleUntil = Time.unscaledTime + 6f;
        bubble.gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (bubble == null || !bubble.gameObject.activeSelf) return;
        if (Time.unscaledTime >= visibleUntil) { bubble.gameObject.SetActive(false); return; }
        Camera camera = Camera.main;
        canvas.enabled = camera != null && Vector3.Distance(camera.transform.position, transform.position) <= 30f;
        if (!canvas.enabled) return;
        float top = body != null ? body.center.y + body.height * .5f : 1.8f;
        bubble.position = transform.TransformPoint(new Vector3(0, top + .38f, 0));
        if (knockdown != null && knockdown.VisualAmount > .1f) bubble.position = knockdown.HeadPosition + Vector3.up * .65f;
        bubble.rotation = camera.transform.rotation;
    }
}
