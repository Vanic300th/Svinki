using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public sealed partial class NetworkPlayer
{
    private readonly SyncVar<bool> cameraOwned = new SyncVar<bool>();
    private readonly SyncVar<bool> cameraEquipped = new SyncVar<bool>();
    public void AwardCamera()
    {
        cameraOwned.Value=true;GetComponent<PlayerPhotoCamera>().Apply(true,false);
    }
    private void SetupPhotoCamera()
    {
        if(GetComponent<PlayerPhotoCamera>()==null)gameObject.AddComponent<PlayerPhotoCamera>();
        cameraOwned.OnChange+=(old,next,server)=>GetComponent<PlayerPhotoCamera>().Apply(next,cameraEquipped.Value);
        cameraEquipped.OnChange+=(old,next,server)=>GetComponent<PlayerPhotoCamera>().Apply(cameraOwned.Value,next);
    }
    private void ApplyPhotoCamera()=>GetComponent<PlayerPhotoCamera>().Apply(cameraOwned.Value,cameraEquipped.Value);
    public void RequestCameraEquip(bool equipped){if(IsOwner)CameraEquipServerRpc(equipped);}
    [ServerRpc(RequireOwnership=true)]
    private void CameraEquipServerRpc(bool equipped)
    {
        var camera=GetComponent<PlayerPhotoCamera>();
        if(equipped && (!cameraOwned.Value || !camera.CanUse))return;
        cameraEquipped.Value=equipped;camera.Apply(cameraOwned.Value,equipped);
    }
    public void StowCamera(){if(IsServerInitialized){cameraEquipped.Value=false;GetComponent<PlayerPhotoCamera>().Apply(cameraOwned.Value,false);}else RequestCameraEquip(false);}
    public void RequestPhoto(){if(IsOwner)PhotoServerRpc();}
    [ServerRpc(RequireOwnership=true)]
    private void PhotoServerRpc()
    {
        var camera=GetComponent<PlayerPhotoCamera>();
        if(!camera.TryShoot())return;
        PhotoObserversRpc();
        PhotoTargetRpc(Owner);
    }
    [ObserversRpc] private void PhotoObserversRpc()=>GetComponent<PlayerPhotoCamera>().ShowFlash();
    [TargetRpc] private void PhotoTargetRpc(FishNet.Connection.NetworkConnection connection)=>GetComponent<PlayerPhotoCamera>().CapturePhoto();
}
