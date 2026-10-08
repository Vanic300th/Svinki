using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
public static class MonkeyToyNetworkCheck
{
 public static string PrepareTarget()
 {
  foreach(var brain in UnityEngine.Object.FindObjectsByType<MannequinBrain>())brain.enabled=false;
  foreach(var thief in UnityEngine.Object.FindObjectsByType<ThiefBrain>())thief.enabled=false;
  var guest=PlayerRegistry.Players.First(p=>!p.IsLocal);var host=PlayerRegistry.Players.First(p=>p.IsLocal);
  var cc=host.GetComponent<CharacterController>();cc.enabled=false;host.transform.position=new Vector3(0,.05f,5);cc.enabled=true;
  var brainTarget=UnityEngine.Object.FindObjectsByType<MannequinBrain>().First();var agent=brainTarget.GetComponent<NavMeshAgent>();agent.Warp(guest.Position+Vector3.forward*1.4f);brainTarget.enabled=true;
  return brainTarget.name;
 }
 public static string Inspect()
 {
  var stub=UnityEngine.Object.FindObjectsByType<MannequinStun>().Single(s=>s.IsStunned);
  if(stub.GetComponent<MannequinBrain>().enabled||stub.GetComponent<NavMeshAgent>().enabled)throw new Exception("Stunned NPC still acts");
  if(stub.GetComponent<ArticulatedRagdoll>().BonePosition("Head").y-stub.transform.position.y>.9f)throw new Exception("NPC does not lie on floor");
  if(MonkeyToy.All.Count!=1||PlayerRegistry.Players.Any(p=>p.GetComponent<PlayerMonkeyCarry>().Held!=null))throw new Exception("Toy not consumed on host");
  return "PASS host: toy consumed, NPC head below .9m, brain/navigation disabled";
 }
}
