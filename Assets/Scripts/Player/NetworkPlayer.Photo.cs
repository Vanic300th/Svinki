using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public sealed partial class NetworkPlayer
{
    private readonly SyncVar<bool> cameraEquipped = new SyncVar<bool>();
    public void AwardCamera()=>ApplyPhotoCamera();
    private void SetupPhotoCamera()
    {
        if(GetComponent<PlayerPhotoCamera>()==null)gameObject.AddComponent<PlayerPhotoCamera>();
        cameraEquipped.OnChange+=(old,next,server)=>GetComponent<PlayerPhotoCamera>().Apply(next);
    }
    private void ApplyPhotoCamera()=>GetComponent<PlayerPhotoCamera>().Apply(cameraEquipped.Value);
    public void RequestCameraEquip(bool equipped){if(IsOwner)CameraEquipServerRpc(equipped);}
    [ServerRpc(RequireOwnership=true)]
    private void CameraEquipServerRpc(bool equipped)
    {
        var camera=GetComponent<PlayerPhotoCamera>();
        if(equipped && !camera.CanUse)return;
        cameraEquipped.Value=equipped;camera.Apply(equipped);
    }
    public void StowCamera(){if(IsServerInitialized){cameraEquipped.Value=false;GetComponent<PlayerPhotoCamera>().Apply(false);}else RequestCameraEquip(false);}
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
