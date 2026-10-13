using System;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class DualLiftSetup
{
 static T[] Items<T>(Scene s) where T:Component=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
 public static string Apply()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode required");
  Scene Open(string name){var s=SceneManager.GetSceneByName(name);return s.IsValid()?s:EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Additive);}
  var factory=Open("SampleScene");var lobby=Open("FriendslopLobby");
  if(factory.isDirty||lobby.isDirty)throw new Exception("Unsaved scene changes");
  if(Items<LobbyFreightElevator>(factory).Length>0)return "Return lift already exists";
  var field=typeof(NetworkObject).GetField("SceneId",BindingFlags.NonPublic|BindingFlags.Instance);
  var original=Items<NetworkObject>(factory).ToDictionary(n=>n,n=>(ulong)field.GetValue(n));
  var old=Items<RoundFinishStation>(factory).Single();
  var source=Items<LobbyFreightElevator>(lobby).Single();
  var copy=UnityEngine.Object.Instantiate(source.gameObject);copy.name="Freight lift · return / final show";
  SceneManager.MoveGameObjectToScene(copy,factory);
  copy.transform.SetParent(factory.GetRootGameObjects().Single(g=>g.name=="Graybox Level").transform,false);
  copy.transform.position=new Vector3(0,0,-7.7f);copy.transform.rotation=Quaternion.Euler(0,180,0);copy.transform.localScale=Vector3.one;
  var station=copy.AddComponent<RoundFinishStation>();var data=new SerializedObject(station);data.FindProperty("zoneCenterOffset").vector3Value=Vector3.zero;data.ApplyModifiedPropertiesWithoutUndo();
  var lift=copy.GetComponent<LobbyFreightElevator>();data=new SerializedObject(lift);data.FindProperty("destinationTitle").stringValue="RETURN LIFT · FINAL SHOW";data.ApplyModifiedPropertiesWithoutUndo();
  foreach(var text in copy.GetComponentsInChildren<TMP_Text>(true))text.text="RETURN LIFT\nENTER TOGETHER";
  UnityEngine.Object.DestroyImmediate(old.gameObject);
  field.SetValue(copy.GetComponent<NetworkObject>(),(ulong)0);
  typeof(NetworkObject).GetMethod("CreateSceneId",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{factory,false,0});
  typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(copy.GetComponent<NetworkObject>(),new object[]{true,false});
  if(original.Any(p=>(ulong)field.GetValue(p.Key)!=p.Value))throw new Exception("Existing IDs changed");
  Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(factory);EditorSceneManager.SaveScene(factory);AssetDatabase.SaveAssets();
  return "PASS: factory return lift installed; "+original.Count+" original network IDs preserved";
 }
}
