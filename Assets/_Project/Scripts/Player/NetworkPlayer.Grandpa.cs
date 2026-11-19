using FishNet.Object.Synchronizing;

public struct GrandpaPlayerProfile
{
    public GrandpaRole Role;
    public string PigClassId;
}

public sealed partial class NetworkPlayer
{
    private readonly SyncVar<GrandpaPlayerProfile> grandpaProfile = new SyncVar<GrandpaPlayerProfile>();
    public GrandpaRole HouseRole => grandpaProfile.Value.Role;
    public string PigClassId => grandpaProfile.Value.PigClassId;

    internal void SetGrandpaRole(GrandpaRole role, string pigClassId)
    {
        grandpaProfile.Value = new GrandpaPlayerProfile
        {
            Role = role,
            PigClassId = role == GrandpaRole.Pig ? pigClassId : null
        };
        ApplyGrandpaProfile(grandpaProfile.Value);
    }

    private void OnGrandpaProfileChanged(GrandpaPlayerProfile previous, GrandpaPlayerProfile current, bool asServer) =>
        ApplyGrandpaProfile(current);

    private void ApplyGrandpaProfile(GrandpaPlayerProfile profile)
    {
        if (motor == null) return;
        motor.PigClassSpeedMultiplier = profile.Role == GrandpaRole.Pig
            ? PigClassPreset.Find(profile.PigClassId)?.MovementMultiplier ?? 1f
            : 1f;
    }
}
