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

    public void SetLocalPlayer(NetworkPlayer player) => localPlayer = player;

    private void Awake() => playerCamera = GetComponent<Camera>();

    private void Update()
    {
        PickupItem next = null;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var physicsScene = localPlayer != null ? localPlayer.gameObject.scene.GetPhysicsScene() : gameObject.scene.GetPhysicsScene();
        if (physicsScene.Raycast(ray.origin, ray.direction, out RaycastHit hit, pickupDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            next = hit.collider.GetComponentInParent<PickupItem>();

        if (next != target)
        {
            if (target != null) target.SetTargeted(false);
            target = next;
            if (target != null) target.SetTargeted(true);
        }

        if (target != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            PickupItem collected = target;
            target = null;
            NetworkPickup networkPickup = collected.GetComponent<NetworkPickup>();
            if (localPlayer != null && networkPickup != null)
                localPlayer.RequestPickup(networkPickup);
            else if (networkPickup == null)
                collected.PickUp(gameObject); // одиночка: вещь уходит в комплект того, кто подобрал
        }
    }

    private void OnGUI()
    {
        if (crosshairStyle == null)
        {
            crosshairStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
            crosshairStyle.normal.textColor = Color.white;
            promptStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            promptStyle.normal.textColor = Color.yellow;
        }

        GUI.Label(new Rect(Screen.width * 0.5f - 12f, Screen.height * 0.5f - 14f, 24f, 28f), "+", crosshairStyle);
        if (target != null)
            GUI.Box(new Rect(Screen.width * 0.5f - 155f, Screen.height * 0.5f + 28f, 310f, 34f),
                "E — подобрать: " + target.ItemName, promptStyle);
    }

    private void OnDisable()
    {
        if (target != null) target.SetTargeted(false);
        target = null;
    }
}
