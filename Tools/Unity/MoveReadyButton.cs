using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class MoveReadyButton
{
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var station = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RoundFinishStation>(true)).Single();
        Vector3 oldPosition = station.transform.position;
        station.transform.position = new Vector3(-6.03f, 1.35f, -7.5f);
        Physics.SyncTransforms();
        // Keep the wall mount flush and the existing gathering-area offset intact.
        if (!Physics.Raycast(station.transform.position, Vector3.left, out var wall, .45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("No wall behind the relocated READY button.");
        var approach = station.transform.position + Vector3.right * 1.5f; approach.y = .05f;
        if (!NavMesh.SamplePosition(approach, out var point, .5f, NavMesh.AllAreas))
            throw new InvalidOperationException("The relocated READY button has no walkable approach.");
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(new Vector3(0, .05f, -3), point.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            throw new InvalidOperationException("The relocated READY button is unreachable from spawn.");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("ArtSource/ReadyPosition");
        File.WriteAllText("ArtSource/ReadyPosition/placement.txt", "READY: " + oldPosition + " -> " + station.transform.position +
            "\nGathering centre: " + station.Center + "\nWall: " + wall.collider.name + "\nWalkable approach: " + point.position + "\nNavigation path from spawn: complete\n");
    }
}
