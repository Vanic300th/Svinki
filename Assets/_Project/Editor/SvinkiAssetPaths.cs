using System;
using UnityEditor;

/// <summary>Paths used by project authoring tools. Runtime resource addresses and scene names stay stable.</summary>
public static class SvinkiAssetPaths
{
    public const string Root = "Assets/_Project";
    public const string Content = Root + "/Content";
    public const string Editor = Root + "/Editor";
    public const string Scripts = Root + "/Scripts";
    public const string Scenes = Root + "/Scenes";
    public const string BootstrapScene = Scenes + "/Bootstrap/Lobby.unity";
    public const string GameplayScene = Scenes + "/Gameplay/SampleScene.unity";
    public const string SocialLobbyScene = Scenes + "/Lobby/FriendslopLobby.unity";
    public const string DevelopmentScene = Scenes + "/Development/testroom.unity";
    public const string DevelopmentNavMesh = Scenes + "/Development/testroom_NavMesh.asset";
    public const string Prefabs = Root + "/Prefabs";
    public const string OnlinePrefabs = Prefabs + "/Online";
    public const string NetworkPlayerPrefab = Prefabs + "/NetworkPlayer.prefab";
    public const string OfflinePlayerPrefab = Prefabs + "/OfflinePlayer.prefab";
    public const string NetworkManagerPrefab = Prefabs + "/SvinkiNetworkManager.prefab";
    public const string MannequinPrefab = Prefabs + "/Mannequin.prefab";
    public const string Characters = Content + "/Characters";
    public const string MannequinModels = Characters + "/Mannequins/Models";
    public const string MannequinAnimations = Characters + "/Mannequins/Animations";
    public const string ClothingRoot = Content + "/Clothing";
    public const string ClothingLegacyModels = ClothingRoot + "/Compatibility/DisplayModels";
    public const string ClothingPigModels = ClothingRoot + "/Models";
    public const string ClothingGenerated = ClothingRoot + "/Generated";
    public const string ClothingDefinitions = ClothingGenerated + "/Definitions";
    public const string ClothingPickups = ClothingGenerated + "/Pickups";
    public const string ClothingGeneratedMaterials = ClothingGenerated + "/Materials";
    public const string ClothingTemplateDefinitions = ClothingRoot + "/Templates/Definitions";
    public const string ClothingTemplateModels = ClothingRoot + "/Templates/Models";
    public const string ClothingTemplateMaterials = ClothingRoot + "/Materials";
    public const string ClothingTextures = ClothingRoot + "/Textures";
    public const string Environment = Content + "/Environment";
    public const string IndustrialArt = Environment + "/IndustrialCC0";
    public const string LevelDesign = Environment + "/LevelDesign";
    public const string LobbyMaterials = Environment + "/Lobby/Materials";
    public const string SharedMaterials = Environment + "/SharedMaterials";
    public const string Items = Content + "/Items";
    public const string Audio = Content + "/Audio";
    public const string Settings = Root + "/Settings";
    public const string Localization = Root + "/Localization";
    public const string Shaders = Root + "/Shaders";
    public const string InputActions = Settings + "/InputSystem_Actions.inputactions";

    public static bool MigrationActive => SessionState.GetBool("Svinki.AssetMigration.Active", false);

    public static string ScenePath(string sceneName)
    {
        switch (sceneName)
        {
            case "Lobby": return BootstrapScene;
            case "SampleScene": return GameplayScene;
            case "FriendslopLobby": return SocialLobbyScene;
            case "testroom": return DevelopmentScene;
            default: throw new ArgumentException("Unknown project scene: " + sceneName, nameof(sceneName));
        }
    }

    /// <summary>Create nested asset folders through Unity so each folder receives its own metadata.</summary>
    public static void EnsureFolder(string folder)
    {
        if (MigrationActive) throw new InvalidOperationException("Asset migration is active; wait before running authoring tools.");
        if (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException("Expected an Assets folder.", nameof(folder));
        string parent = "Assets";
        foreach (string part in folder.Substring("Assets".Length).TrimStart('/').Split('/'))
        {
            if (part.Length == 0) continue;
            string path = parent + "/" + part;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, part);
            parent = path;
        }
    }
}
