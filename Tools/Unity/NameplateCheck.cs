using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FishNet.Object.Synchronizing;

public static class NameplateCheck
{
    static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static string Spawn()
    {
        var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var obj=UnityEngine.Object.Instantiate(prefab,new Vector3(0,.05f,1),Quaternion.Euler(0,180,0));obj.name="Nickname test remote";
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj,Camera.main.gameObject.scene);
        obj.GetComponent<NetworkPlayer>().SetIdentity("nickname-test","Проверка ника");
        UnityEngine.Object.FindAnyObjectByType<FishNet.Managing.NetworkManager>().ServerManager.Spawn(obj);
        return "Unowned network player spawned for observer nameplate check.";
    }
    public static string Standing()=>Stance(0);
    public static string HalfCrouch()=>Stance(.5f);
    public static string Crouching()=>Stance(1);
    public static string ObserverStanding()=>Observer(1.7f);
    public static string ObserverCrouching()=>Observer(1f);
    static string Observer(float height)
    {
        var camera=Camera.main;
        camera.GetComponent<GrayboxFirstPersonCamera>().enabled=false;
        var owner=UnityEngine.Object.FindObjectsByType<NetworkPlayer>().Single(p=>p.IsOwner);
        owner.enabled=false;
        camera.transform.SetPositionAndRotation(new Vector3(0,height,-2),Quaternion.identity);
        NetworkLobby.Instance.MenuVisible=false;
        Cursor.lockState=CursorLockMode.Locked;
        return "Observer eye height: "+height;
    }
    public static string Mohawk()
    {
        var player=UnityEngine.Object.FindObjectsByType<NetworkPlayer>().Single(p=>p.name=="Nickname test remote");
        var face=PigFace.Decode(player.GetComponentInChildren<PigAppearance>().FaceCode);face.mohawk=true;
        player.SetAppearance(face.Encode());
        return "Mohawk enabled on the remote test avatar.";
    }
    static string Stance(float crouch)
    {
        var player=UnityEngine.Object.FindObjectsByType<NetworkPlayer>().Single(p=>p.name=="Nickname test remote");
        ((SyncVar<PlayerPose>)typeof(NetworkPlayer).GetField("pose",flags).GetValue(player)).Value=new PlayerPose{Yaw=180,Crouch=crouch,Grounded=true};
        player.GetComponent<GrayboxPlayerController>().SetRemoteStance(crouch);
        var animator=player.transform.Find("Legacy Animation Driver").GetComponent<Animator>();animator.SetBool("Grounded",true);animator.SetFloat("Crouch",crouch);animator.Play(crouch>.5f?"Crouch":"Locomotion",0,0);animator.Update(0);
        var pig=player.GetComponentInChildren<PigAppearance>();typeof(PigMotion).GetMethod("LateUpdate",flags).Invoke(pig.GetComponent<PigMotion>(),null);
        typeof(PlayerNameplate).GetMethod("LateUpdate",flags).Invoke(player.GetComponent<PlayerNameplate>(),null);
        var label=player.transform.Find("Nickname").GetComponent<TMPro.TextMeshProUGUI>();label.ForceMeshUpdate();
        float bottom=label.transform.position.y-label.rectTransform.rect.height*label.transform.lossyScale.y*.5f;
        float top=pig.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).Max(r=>r.bounds.max.y);
        if(!label.GetComponent<Canvas>().enabled||bottom<top+.1f)throw new Exception("Nickname is disabled or overlaps the visible avatar.");
        return UnityEngine.JsonUtility.ToJson(new Result{crouch=crouch,enabled=label.GetComponent<Canvas>().enabled,labelBottom=bottom,avatarTop=top,height=label.transform.position.y});
    }
    public static string LightsAndOwner()
    {
        var owner=UnityEngine.Object.FindObjectsByType<NetworkPlayer>().Single(p=>p.IsOwner);
        var ownLabel=owner.transform.Find("Nickname").GetComponent<Canvas>();
        var lights=UnityEngine.Object.FindObjectsByType<Light>().Where(l=>l.name=="Remote flashlight"||l.GetComponent<FlashlightController>()!=null).ToArray();
        if(ownLabel.enabled||lights.Length<2||lights.Any(l=>Mathf.Abs(l.intensity-84)>.01f))throw new Exception("Flashlight intensity or owner nameplate visibility is incorrect.");
        return "Local and remote flashlights: 84; own nickname hidden.";
    }
    [Serializable] class Result{public float crouch,labelBottom,avatarTop,height;public bool enabled;}
}
