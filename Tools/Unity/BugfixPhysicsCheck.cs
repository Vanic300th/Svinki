// Run only in a temporary verification project: this check replaces open scenes.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class BugfixPhysicsCheck
{
 [Serializable] public class Trial { public Vector3 start, end; public bool trapped; }
 [Serializable] public class Report { public Vector3 hoop; public string[] colliders; public Trial[] trials; }
 public static void Inspect()
 {
  var scene=EditorSceneManager.OpenScene(SvinkiAssetPaths.SocialLobbyScene,OpenSceneMode.Single);
  var hoop=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BasketballHoop>(true)).Single();
  hoop.SendMessage("Awake",SendMessageOptions.DontRequireReceiver);
  var ball=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LobbyProp>(true)).First(p=>p.Kind==LobbyPropKind.Basketball);
  var body=ball.GetComponent<Rigidbody>(); body.isKinematic=false;body.useGravity=true;
  var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
  var trials=new List<Trial>();
  try
  {
   foreach(float x in new[]{-.18f,0f,.18f})foreach(float z in new[]{-.35f,-.16f,-.04f,.08f,.2f,.6f})
   {
    var start=new Vector3(x,4.1f,z);body.position=hoop.transform.TransformPoint(start);body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.WakeUp();Physics.SyncTransforms();
    for(int i=0;i<500;i++)Physics.Simulate(.02f);
    var end=hoop.transform.InverseTransformPoint(body.position);trials.Add(new Trial{start=start,end=end,trapped=end.y>2.4f});
   }
  }
  finally{Physics.simulationMode=mode;}
  var report=new Report{hoop=hoop.transform.position,colliders=hoop.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger).Select(c=>c.name+" local="+c.transform.localPosition+" bounds="+c.bounds).ToArray(),trials=trials.ToArray()};
  Directory.CreateDirectory("ArtSource/Bugfix");File.WriteAllText("ArtSource/Bugfix/geometry.json",JsonUtility.ToJson(report,true));
  Debug.Log("BUGFIX GEOMETRY: "+trials.Count(t=>t.trapped)+" / "+trials.Count+" trapped drops.");
 }
}
