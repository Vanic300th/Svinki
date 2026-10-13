using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>The existing finish anchor, now connected to the factory return lift.</summary>
public sealed class RoundFinishStation : MonoBehaviour
{
    [SerializeField] private Vector3 zoneCenterOffset = new Vector3(0, -.95f, -2.2f);
    [SerializeField] private float radius = 3.3f;
    private static readonly List<RoundFinishStation> stations = new List<RoundFinishStation>();
    public LobbyFreightElevator Elevator => GetComponent<LobbyFreightElevator>();
    public Vector3 Center => Elevator != null ? Elevator.transform.position : transform.position + zoneCenterOffset;
    private void OnEnable() { if (!stations.Contains(this)) stations.Add(this); }
    private void OnDisable() => stations.Remove(this);
    public static RoundFinishStation ForScene(Scene scene) => stations.Find(s => s != null && s.gameObject.scene == scene);
    public bool Contains(PlayerAvatar player)
    {
        if (player == null || !player.IsAlive || player.gameObject.scene != gameObject.scene || player.GetComponent<PlayerKnockdown>()?.IsDown == true) return false;
        if (Elevator != null) return Elevator.Contains(player.Position);
        Vector3 delta = player.Position - Center;
        return Mathf.Abs(delta.y) < 1.8f && new Vector2(delta.x, delta.z).sqrMagnitude <= radius * radius;
    }
    public void Press()
    {
        if (Elevator != null) return;
        var lobby = NetworkLobby.Instance;
        if (lobby != null) lobby.FinishReady(!lobby.IsFinishReady);
    }
    private void Start()
    {
        if (Elevator != null) return;
        var labelObject = new GameObject("Ready button label"); labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0, 0, -.205f);
        var label = labelObject.AddComponent<TextMeshPro>();
        label.font = PlayerChat.Instance.Font; GameLanguage.Set(label, "READY");
        label.fontSize = 1.15f; label.alignment = TextAlignmentOptions.Center; label.color = new Color(.02f, .09f, .04f);
        label.rectTransform.sizeDelta = new Vector2(.43f, .23f);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => stations.Clear();
}
