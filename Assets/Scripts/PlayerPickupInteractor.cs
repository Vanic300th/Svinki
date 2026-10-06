using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class PlayerPickupInteractor : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float pickupDistance = 3.5f;

    private Camera playerCamera;
    private PickupItem target;
    private GUIStyle promptStyle;
    private GUIStyle crosshairStyle;
    private NetworkPlayer localPlayer;
    private Vector3 targetPoint;
    private ThrowableMannequin mannequinTarget;

    private PlayerMannequinCarry Hands
    {
        get
        {
            if (localPlayer != null) return localPlayer.GetComponent<PlayerMannequinCarry>();
            foreach (PlayerAvatar player in PlayerRegistry.Players)
                if (player != null && player.IsLocal) return player.GetComponent<PlayerMannequinCarry>();
            return null;
        }
    }

    public void SetLocalPlayer(NetworkPlayer player) => localPlayer = player;

    private void Awake() => playerCamera = GetComponent<Camera>();

    private void Update()
    {
        if (PlayerChat.BlocksInput || NetworkLobby.Instance != null && !NetworkLobby.Instance.InputAllowed) { if (target != null) target.SetTargeted(false); target = null; mannequinTarget = null; return; }
        var hands = Hands;
        if (hands != null && hands.GetComponent<PlayerKnockdown>()?.IsDown == true)
        {
            if (target != null) target.SetTargeted(false);
            target = null; mannequinTarget = null; return;
        }
        if (hands != null && hands.Held != null)
        {
            if (target != null) target.SetTargeted(false);
            target = null; mannequinTarget = null;
            if (Mouse.current?.leftButton.wasPressedThisFrame == true) hands.Release(true);
            else if (Mouse.current?.rightButton.wasPressedThisFrame == true || Keyboard.current?.eKey.wasPressedThisFrame == true) hands.Release(false);
            return;
        }
        PickupItem next = null;
        mannequinTarget = null;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var physicsScene = localPlayer != null ? localPlayer.gameObject.scene.GetPhysicsScene() : gameObject.scene.GetPhysicsScene();
        if (physicsScene.Raycast(ray.origin, ray.direction, out RaycastHit hit, pickupDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            next = hit.collider.GetComponentInParent<PickupItem>();
            mannequinTarget = hit.collider.GetComponentInParent<ThrowableMannequin>();
            targetPoint = hit.point;
        }

        if (mannequinTarget != null && !mannequinTarget.IsHeld && Keyboard.current?.eKey.wasPressedThisFrame == true)
        {
            var network = mannequinTarget.GetComponent<NetworkThrowableMannequin>();
            if (localPlayer != null && network != null && network.isActiveAndEnabled)
                localPlayer.RequestMannequinGrab(network, targetPoint);
            else if (hands != null) mannequinTarget.TryGrab(hands.GetComponent<PlayerAvatar>());
            return;
        }

        if (next != target)
        {
            if (target != null) target.SetTargeted(false);
            target = next;
            if (target != null) target.SetTargeted(true);
        }

        if (target != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            ClothingPickup clothing = target.GetComponent<ClothingPickup>();
            if (clothing != null && !clothing.CanCollect(gameObject)) return;
            PickupItem collected = target;
            collected.SetTargeted(false);
            target = null;
            NetworkPickup networkPickup = collected.GetComponent<NetworkPickup>();
            bool online = networkPickup != null && networkPickup.isActiveAndEnabled;
            if (localPlayer != null && online)
                localPlayer.RequestPickup(networkPickup, targetPoint);
            else if (!online)
                collected.PickUp(gameObject); // одиночка: вещь уходит в комплект того, кто подобрал
        }
    }

    private void OnGUI()
    {
        if (PlayerChat.BlocksInput || NetworkLobby.Instance != null && !NetworkLobby.Instance.InputAllowed) return;
        if (crosshairStyle == null)
        {
            crosshairStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
            crosshairStyle.normal.textColor = Color.white;
            promptStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            promptStyle.normal.textColor = Color.yellow;
        }

        GUI.Label(new Rect(Screen.width * 0.5f - 12f, Screen.height * 0.5f - 14f, 24f, 28f), "+", crosshairStyle);
        if (Hands?.GetComponent<PlayerKnockdown>()?.IsDown == true)
        {
            GUI.Box(new Rect(Screen.width * .5f - 155, Screen.height * .5f + 28, 310, 34), "Вас сбили! Поднимаемся…", promptStyle);
            return;
        }
        if (Hands?.Held != null)
            GUI.Box(new Rect(Screen.width * .5f - 215, Screen.height * .5f + 28, 430, 34),
                "ЛКМ — бросить • ПКМ / E — отпустить", promptStyle);
        else if (mannequinTarget != null && !mannequinTarget.IsHeld)
            GUI.Box(new Rect(Screen.width * .5f - 155, Screen.height * .5f + 28, 310, 34), "E — взять манекен", promptStyle);
        if (target != null)
        {
            ClothingPickup clothing = target.GetComponent<ClothingPickup>();
            bool canCollect = clothing == null || clothing.CanCollect(gameObject);
            GUI.Box(new Rect(Screen.width * 0.5f - 155f, Screen.height * 0.5f + 28f, 310f, 34f),
                canCollect ? "E — подобрать: " + target.ItemName : "Этот слот одежды уже занят", promptStyle);
        }
    }

    private void OnDisable()
    {
        if (target != null) target.SetTargeted(false);
        target = null;
        mannequinTarget = null;
    }
}
