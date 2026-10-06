using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>The finish button and gathering area in the level's entrance.</summary>
public sealed class RoundFinishStation : MonoBehaviour
{
    [SerializeField] private Vector3 zoneCenterOffset = new Vector3(0, -.95f, -2.2f);
    [SerializeField] private float radius = 3.3f;
    private static readonly List<RoundFinishStation> stations = new List<RoundFinishStation>();
    public Vector3 Center => transform.position + zoneCenterOffset;
    private void OnEnable() { if (!stations.Contains(this)) stations.Add(this); }
    private void OnDisable() => stations.Remove(this);
    public static RoundFinishStation ForScene(Scene scene) => stations.Find(s => s != null && s.gameObject.scene == scene);
    public bool Contains(PlayerAvatar player)
    {
        if (player == null || player.gameObject.scene != gameObject.scene || player.GetComponent<PlayerKnockdown>()?.IsDown == true) return false;
        Vector3 delta = player.Position - Center;
        return Mathf.Abs(delta.y) < 1.8f && new Vector2(delta.x, delta.z).sqrMagnitude <= radius * radius;
    }
    public void Press()
    {
        var lobby = NetworkLobby.Instance;
        if (lobby != null) lobby.FinishReady(!lobby.IsFinishReady);
    }
    private void Start()
    {
        var labelObject = new GameObject("Ready button label"); labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0, .15f, -.225f);
        var label = labelObject.AddComponent<TextMeshPro>();
        label.font = PlayerChat.Instance.Font; label.text = "READY";
        label.fontSize = 2.3f; label.alignment = TextAlignmentOptions.Center; label.color = new Color(.02f, .09f, .04f);
        label.rectTransform.sizeDelta = new Vector2(1.45f, .45f);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => stations.Clear();
}
