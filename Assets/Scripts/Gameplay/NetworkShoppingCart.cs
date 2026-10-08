using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(ShoppingCart))]
public sealed class NetworkShoppingCart : NetworkBehaviour
{
    private readonly SyncVar<NetworkObject> driver = new SyncVar<NetworkObject>();
    private readonly SyncVar<NetworkObject> rider = new SyncVar<NetworkObject>();
    [System.Serializable] private struct CargoState { public string[] ids; public int revision; }
    private readonly SyncVar<string> cargoState = new SyncVar<string>();
    private CartCargo cargo;
    private ShoppingCart cart;
    public bool ServerActive => NetworkObject != null && IsServerInitialized;
    private void Awake()
    {
        cart = GetComponent<ShoppingCart>(); cargo = GetComponent<CartCargo>();
        cargoState.OnChange += (previous, next, asServer) => { if (!asServer && !IsServerInitialized) ApplyCargo(); };
        driver.OnChange += Changed; rider.OnChange += Changed;
    }
    public override void OnStartServer() { base.OnStartServer(); cart.EnablePhysics(true); }
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsServerInitialized) { cart.EnablePhysics(false); Apply(); ApplyCargo(); }
    }
    public void SetOccupants(PlayerAvatar pushing, PlayerAvatar seated)
    {
        if (!ServerActive) return;
        driver.Value = pushing != null ? pushing.GetComponent<NetworkObject>() : null;
        rider.Value = seated != null ? seated.GetComponent<NetworkObject>() : null;
    }
    public void SetCargo(string[] ids, int revision)
    {
        if (ServerActive) cargoState.Value = JsonUtility.ToJson(new CargoState { ids = ids, revision = revision });
    }
    private void ApplyCargo()
    {
        if (cargo == null || string.IsNullOrEmpty(cargoState.Value)) return;
        var state = JsonUtility.FromJson<CargoState>(cargoState.Value); cargo.Apply(state.ids, state.revision);
    }
    private void Changed(NetworkObject previous, NetworkObject next, bool asServer)
    { if (!asServer && !IsServerInitialized) Apply(); }
    private void Apply() => cart.ApplyOccupants(driver.Value != null ? driver.Value.GetComponent<PlayerAvatar>() : null,
        rider.Value != null ? rider.Value.GetComponent<PlayerAvatar>() : null);
    public override void OnStopClient() { cart.ApplyOccupants(null, null); base.OnStopClient(); }
}
