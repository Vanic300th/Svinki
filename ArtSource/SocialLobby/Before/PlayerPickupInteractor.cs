using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class PlayerPickupInteractor : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float pickupDistance = 3.5f;

    private Camera playerCamera;
    private PickupItem target;
    [SerializeField] private GameHud hud;
    private NetworkPlayer localPlayer;
    private Vector3 targetPoint;
    private ThrowableMannequin mannequinTarget;
    private RoundFinishStation finishTarget;
    private ShoppingCart cartTarget;
    private MonkeyToy monkeyTarget;
    private PlayerMonkeyCarry MonkeyHands => LocalAvatar != null ? LocalAvatar.GetComponent<PlayerMonkeyCarry>() : null;
    private PlayerPhotoCamera PhotoCamera => LocalAvatar?.GetComponent<PlayerPhotoCamera>();
    private PlayerAvatar LocalAvatar => localPlayer != null ? localPlayer.GetComponent<PlayerAvatar>() : Hands?.GetComponent<PlayerAvatar>();

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
        monkeyTarget = null;
        if (EmoteWheel.BlocksInput || PlayerChat.BlocksInput || NetworkLobby.Instance != null && !NetworkLobby.Instance.InputAllowed) { if (target != null) target.SetTargeted(false); target = null; mannequinTarget = null; finishTarget = null; cartTarget = null; return; }
        var hands = Hands;
        if (hands != null && hands.GetComponent<PlayerKnockdown>()?.IsDown == true)
        {
            if (target != null) target.SetTargeted(false);
            target = null; mannequinTarget = null; finishTarget = null; cartTarget = null; return;
        }
        var cart = ShoppingCart.For(LocalAvatar);
        if (cart != null && Keyboard.current?.gKey.wasPressedThisFrame == true)
        { CartCargoView.Instance?.Open(cart.GetComponent<CartCargo>()); return; }
        if (cart != null)
        {
            if (target != null) target.SetTargeted(false);
            target = null; mannequinTarget = null; finishTarget = null; cartTarget = null;
            if (Keyboard.current?.eKey.wasPressedThisFrame == true || cart.Rider == LocalAvatar && Keyboard.current?.spaceKey.wasPressedThisFrame == true)
                UseCart(cart, false, false);
            else if (cart.Driver == LocalAvatar && Mouse.current?.leftButton.wasPressedThisFrame == true)
                UseCart(cart, false, true);
            if (localPlayer == null && cart.Driver == LocalAvatar)
                cart.SubmitInput(LocalAvatar, GrayboxPlayerController.ReadMoveInput(), Keyboard.current?.leftShiftKey.isPressed == true);
            return;
        }
        if(PhotoCamera?.Equipped==true)
        {
            if(target!=null)target.SetTargeted(false);target=null;mannequinTarget=null;finishTarget=null;cartTarget=null;
            if(Mouse.current?.leftButton.wasPressedThisFrame==true)PhotoCamera.Shoot();
            else if(Mouse.current?.rightButton.wasPressedThisFrame==true||Keyboard.current?.eKey.wasPressedThisFrame==true)PhotoCamera.Stow();
            return;
        }
        if (MonkeyHands?.Held != null)
        {
            if (target != null) target.SetTargeted(false);
            target = null; mannequinTarget = null; finishTarget = null; cartTarget = null;
            if (Mouse.current?.leftButton.wasPressedThisFrame == true) MonkeyHands.Swing();
            else if (Mouse.current?.rightButton.wasPressedThisFrame == true || Keyboard.current?.eKey.wasPressedThisFrame == true) MonkeyHands.Release();
            return;
        }
        if (hands != null && hands.Held != null)
        {
            if (target != null) target.SetTargeted(false);
            target = null; mannequinTarget = null; finishTarget = null; cartTarget = null;
            if (Mouse.current?.leftButton.wasPressedThisFrame == true) hands.Release(true);
            else if (Mouse.current?.rightButton.wasPressedThisFrame == true || Keyboard.current?.eKey.wasPressedThisFrame == true) hands.Release(false);
            return;
        }
        PickupItem next = null;
        mannequinTarget = null;
        finishTarget = null; cartTarget = null;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var physicsScene = localPlayer != null ? localPlayer.gameObject.scene.GetPhysicsScene() : gameObject.scene.GetPhysicsScene();
        if (physicsScene.Raycast(ray.origin, ray.direction, out RaycastHit hit, pickupDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            next = hit.collider.GetComponentInParent<PickupItem>();
            mannequinTarget = hit.collider.GetComponentInParent<ThrowableMannequin>();
            finishTarget = hit.collider.GetComponentInParent<RoundFinishStation>();
            cartTarget = hit.collider.GetComponentInParent<ShoppingCart>();
            monkeyTarget = hit.collider.GetComponentInParent<MonkeyToy>();
            var proxyOwner = hit.collider.GetComponentInParent<RagdollColliderOwner>();
            if (monkeyTarget == null && proxyOwner?.Owner != null) monkeyTarget = proxyOwner.Owner.GetComponent<MonkeyToy>();
            targetPoint = hit.point;
        }

        if (cartTarget != null && Keyboard.current?.gKey.wasPressedThisFrame == true)
        { CartCargoView.Instance?.Open(cartTarget.GetComponent<CartCargo>()); return; }
        if (cartTarget != null && (Keyboard.current?.eKey.wasPressedThisFrame == true || Keyboard.current?.rKey.wasPressedThisFrame == true))
        {
            if (target != null) target.SetTargeted(false); target = null;
            UseCart(cartTarget, Keyboard.current.rKey.wasPressedThisFrame, false); return;
        }
        if (finishTarget != null && Keyboard.current?.eKey.wasPressedThisFrame == true)
        { finishTarget.Press(); return; }

        if (monkeyTarget != null && !monkeyTarget.IsHeld && Keyboard.current?.eKey.wasPressedThisFrame == true)
        {
            if (localPlayer != null) localPlayer.RequestMonkeyGrab(monkeyTarget.GetComponent<NetworkMonkeyToy>());
            else monkeyTarget.TryGrab(LocalAvatar);
            return;
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
            if(target.GetComponent<PhotoCameraPickup>()!=null&&PhotoCamera?.Owned==true)return;
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

    private void UseCart(ShoppingCart cart, bool sit, bool launch)
    {
        if (localPlayer != null) localPlayer.RequestCart(cart, sit, launch);
        else if (cart != null && LocalAvatar != null)
        {
            if (ShoppingCart.For(LocalAvatar) == cart) cart.Release(LocalAvatar, launch);
            else cart.TryUse(LocalAvatar, sit);
        }
    }
    private void LateUpdate()
    {
        if (hud == null) hud = GameHud.Find(this);
        if (hud == null) return;
        if (EmoteWheel.BlocksInput || PlayerChat.BlocksInput || NetworkLobby.Instance != null && !NetworkLobby.Instance.InputAllowed)
        {
            hud.SetCrosshair(false);
            hud.HidePrompt();
            return;
        }
        hud.SetCrosshair(true);
        if (Hands?.GetComponent<PlayerKnockdown>()?.IsDown == true)
        {
            hud.ShowPrompt("You got knocked down! Getting up…");
            return;
        }
        var cart = ShoppingCart.For(LocalAvatar);
        if (cart != null)
        {
            hud.ShowPrompt(cart.Driver == LocalAvatar
                ? "W/S — push • A/D — steer • Shift — sprint\nLMB — launch • E — release • G — storage"
                : "You're riding! E / Space — hop out • G — storage");
            return;
        }
        if(PhotoCamera?.Equipped==true){hud.HidePrompt();return;}
        string text = null;
        if (MonkeyHands?.Held != null)
            text = "LMB — hit with monkey • RMB / E — release\nOne hit only · Stuns aggressive mannequins for 30 seconds";
        else if (monkeyTarget != null && !monkeyTarget.IsHeld)
            text = "E — grab monkey toy";
        else if (cartTarget != null)
            text = cartTarget.Speed > 3 ? "The cart is moving too fast" : "E — push cart • R — ride • G — storage";
        else if (Hands?.Held != null)
            text = "LMB — throw • RMB / E — release";
        else if (mannequinTarget != null && !mannequinTarget.IsHeld)
            text = "E — grab mannequin";
        else if (finishTarget != null)
            text = NetworkLobby.Instance?.IsFinishReady == true ? "E — cancel ready" : "E — press Ready";
        if (target != null)
        {
            ClothingPickup clothing = target.GetComponent<ClothingPickup>();
            bool canCollect = clothing == null || clothing.CanCollect(gameObject);
            text = target.GetComponent<PhotoCameraPickup>()!=null ? PhotoCamera?.Owned==true?"You already have a camera":"E — pick up camera · K — equip · P — album" : canCollect ? "E — pick up: " + target.ItemName : "This clothing slot is already occupied";
        }
        hud.ShowPrompt(text);
    }

    private void OnDisable()
    {
        if (target != null) target.SetTargeted(false);
        target = null;
        mannequinTarget = null;
        finishTarget = null; cartTarget = null;
        if (hud != null) { hud.SetCrosshair(false); hud.HidePrompt(); }
    }
}
