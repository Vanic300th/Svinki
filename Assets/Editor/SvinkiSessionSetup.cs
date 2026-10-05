#if UNITY_EDITOR
using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SvinkiSessionSetup
{
    [MenuItem("Svinki/Configure friend sessions")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        const string playerPath = "Assets/Prefabs/NetworkPlayer.prefab";
        var player = PrefabUtility.LoadPrefabContents(playerPath);
        var objectData = new SerializedObject(player.GetComponent<NetworkObject>());
        objectData.FindProperty("_preventDespawnOnDisconnect").boolValue = true; objectData.ApplyModifiedPropertiesWithoutUndo();
        var transformData = new SerializedObject(player.GetComponent<NetworkTransform>());
        transformData.FindProperty("_clientAuthoritative").boolValue = true;
        transformData.FindProperty("_sendToOwner").boolValue = false; transformData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        // Create an offline prefab before Awake, so PlayerAvatar never caches a network view source.
        Object.DestroyImmediate(player.GetComponent<NetworkPlayer>());
        Object.DestroyImmediate(player.GetComponent<NetworkTransform>());
        Object.DestroyImmediate(player.GetComponent<NetworkObject>());
        if (player.GetComponent<PlayerAvatar>() == null) player.AddComponent<PlayerAvatar>();
        var offline = PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/OfflinePlayer.prefab");
        PrefabUtility.UnloadPrefabContents(player);
        const string managerPath = "Assets/Prefabs/SvinkiNetworkManager.prefab";
        var root = PrefabUtility.LoadPrefabContents(managerPath);
        var multipass = root.GetComponent<Multipass>() ?? root.AddComponent<Multipass>();
        var transports = new List<Transport> { root.GetComponent<Tugboat>() ?? root.AddComponent<Tugboat>() };
#if !UNITY_WEBGL && !EOS_DISABLE
        transports.Add(root.GetComponent<FishNet.Transporting.FishyEOSPlugin.FishyEOS>() ?? root.AddComponent<FishNet.Transporting.FishyEOSPlugin.FishyEOS>());
#endif
        var transportData = new SerializedObject(multipass);
        var list = transportData.FindProperty("_transports"); list.arraySize = transports.Count;
        for (int i = 0; i < transports.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = transports[i];
        transportData.ApplyModifiedPropertiesWithoutUndo();
        (root.GetComponent<TransportManager>() ?? root.AddComponent<TransportManager>()).Transport = multipass;
        var lobby = new SerializedObject(root.GetComponent<NetworkLobby>());
        lobby.FindProperty("offlinePlayerPrefab").objectReferenceValue = offline;
        lobby.FindProperty("menuFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        lobby.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, managerPath); PrefabUtility.UnloadPrefabContents(root);
        var web = UnityEditor.Build.NamedBuildTarget.WebGL;
        var defines = new HashSet<string>(PlayerSettings.GetScriptingDefineSymbols(web).Split(';')) { "EOS_DISABLE" };
        defines.Remove(""); PlayerSettings.SetScriptingDefineSymbols(web, string.Join(";", defines));
        AssetDatabase.SaveAssets();
        Debug.Log("Friend session prefabs configured; EOS disabled for web builds.");
    }
}
#endif
