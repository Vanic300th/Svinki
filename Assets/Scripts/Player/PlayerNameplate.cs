using TMPro;
using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(1200)]
public sealed class PlayerNameplate : MonoBehaviour
{
    private NetworkPlayer player;
    private CharacterController body;
    private PlayerKnockdown knockdown;
    private Renderer[] avatarParts;
    private Canvas canvas;
    private TextMeshProUGUI label;
    private void Awake()
    {
        player = GetComponent<NetworkPlayer>();
        body = GetComponent<CharacterController>();
        knockdown = GetComponent<PlayerKnockdown>();
        var pig = GetComponentInChildren<PigAppearance>(true);
        avatarParts = pig != null ? pig.ModelRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
    }
    private void LateUpdate()
    {
        if (player == null || !player.IsClientStarted || PlayerChat.Instance == null) return;
        if (canvas == null)
        {
            var obj = new GameObject("Nickname", typeof(RectTransform), typeof(Canvas), typeof(TextMeshProUGUI));
            obj.transform.SetParent(transform, false);
            canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(320, 44);
            rect.localScale = Vector3.one * .0045f;
            label = obj.GetComponent<TextMeshProUGUI>(); label.font = PlayerChat.Instance.Font;
            label.fontSize = 27; label.color = new Color(.88f, .94f, 1);
            label.alignment = TextAlignmentOptions.Center; label.richText = false; label.raycastTarget = false;
        }
        Camera camera = Camera.main;
        canvas.enabled = !player.IsOwner && camera != null && Vector3.Distance(camera.transform.position, transform.position) < 25;
        if (!canvas.enabled) return;
        label.text = string.IsNullOrWhiteSpace(player.ParticipantName) ? "Piggy" : player.ParticipantName;
        canvas.transform.rotation = camera.transform.rotation;
        Vector3 anchor = knockdown != null ? knockdown.HeadPosition : transform.position;
        bool fallen = knockdown != null && knockdown.VisualAmount > .1f;
        float capsuleTop = body != null ? body.center.y + body.height * .5f : 1.8f;
        float top = fallen ? anchor.y : transform.TransformPoint(new Vector3(0, capsuleTop, 0)).y;
        // The crouched collision capsule can sit below the animated head, ears or mohawk.
        // Read the visible model after the camera, animation and ragdoll have updated.
        foreach (Renderer part in avatarParts)
            if (part != null && part.enabled && part.gameObject.activeInHierarchy)
                top = Mathf.Max(top, part.bounds.max.y);
        float halfHeight = label.rectTransform.rect.height * Mathf.Abs(label.transform.lossyScale.y) * .5f;
        anchor.y = top + .22f + halfHeight;
        canvas.transform.position = anchor;
    }
}
