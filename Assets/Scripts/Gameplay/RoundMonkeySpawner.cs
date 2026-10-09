using System.Linq;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;
public sealed class RoundMonkeySpawner : MonoBehaviour
{
    [SerializeField]private NetworkObject prefab;
    [SerializeField]private NetworkObject cameraPrefab;
    private bool begun;
    public int SpawnedCount { get; private set; }
    private System.Collections.IEnumerator Start()
    {
        yield return null;
        if(NetworkLobby.Instance==null && !FishNet.InstanceFinder.IsServerStarted && !FishNet.InstanceFinder.IsClientStarted)
            Spawn(PlayerRegistry.Players.Count(p=>p!=null&&p.gameObject.scene==gameObject.scene));
    }
    public static void Begin(Scene scene,int players)
    {
        var spawner=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RoundMonkeySpawner>(true)).FirstOrDefault();
        if(spawner!=null)spawner.Spawn(players);
    }
    private void Spawn(int players)
    {
        if(begun||prefab==null)return;begun=true;
        bool offline=NetworkLobby.Instance==null||NetworkLobby.Instance.Offline;
        if(!offline&&!FishNet.InstanceFinder.IsServerStarted)return;
        var layout=RoundWorldLayout.Begin(gameObject.scene);
        for(int i=0;i<Mathf.Clamp(players,1,NetworkLobby.Capacity);i++)
        {
            if(layout==null || !layout.TryTakePoint("Monkey "+i,.65f,out var point))throw new System.InvalidOperationException("No safe monkey spawn");
            var toy=Instantiate(prefab,point+Vector3.up*.12f,Quaternion.Euler(0,180,0));
            SceneManager.MoveGameObjectToScene(toy.gameObject,gameObject.scene);
            if(offline){foreach(var net in toy.GetComponents<NetworkBehaviour>())net.enabled=false;toy.SetIsNetworked(false);toy.gameObject.SetActive(true);}
            else FishNet.InstanceFinder.ServerManager.Spawn(toy,null,gameObject.scene);
            SpawnedCount++;
            if(cameraPrefab!=null)
            {
                if(!layout.TryTakePoint("Camera "+i,.45f,out var cameraPoint))throw new System.InvalidOperationException("No safe camera spawn");
                var camera=Instantiate(cameraPrefab,cameraPoint+Vector3.up*.30f,Quaternion.identity);
                SceneManager.MoveGameObjectToScene(camera.gameObject,gameObject.scene);
                if(offline){foreach(var net in camera.GetComponents<NetworkBehaviour>())net.enabled=false;camera.SetIsNetworked(false);camera.gameObject.SetActive(true);}
                else FishNet.InstanceFinder.ServerManager.Spawn(camera,null,gameObject.scene);
            }
        }
    }
}
