public enum SessionGameMode : byte { BecomeAModel, GrandpasFridge }

/// <summary>Stable mode identifiers shared by the lift, session, and checkpoint.</summary>
public static class SessionGameModes
{
    public const string ModelScene = "SampleScene";
    public const string GrandpaScene = "GrandpasHouse";
    public static bool IsValid(SessionGameMode mode) => mode == SessionGameMode.BecomeAModel || mode == SessionGameMode.GrandpasFridge;
    public static string Title(SessionGameMode mode) => mode == SessionGameMode.GrandpasFridge ? "Grandpa's Fridge" : "Become a Model";
    public static string SceneName(SessionGameMode mode) => mode == SessionGameMode.GrandpasFridge ? GrandpaScene : ModelScene;
    public static bool IsGameplayScene(string name) => name == ModelScene || name == GrandpaScene;
}
