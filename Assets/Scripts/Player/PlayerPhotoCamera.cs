using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Round-only equipment. The host validates equipment and shutter cooldown; photos have no combat effect.</summary>
[DefaultExecutionOrder(290)]
public sealed class PlayerPhotoCamera : MonoBehaviour
{
    public const float Cooldown=1, PhotoFieldOfView=45;
    public bool Owned {get;private set;}
    public bool Equipped {get;private set;}
    public int Shots {get;private set;}
    public float CooldownRemaining=>Mathf.Max(0,nextShot-Time.time);
    private float nextShot,flashUntil;
    private PlayerAvatar avatar;private NetworkPlayer network;
    private Transform model;private Light flash;private Material shell,dark,accent;
    public bool CanUse=>avatar!=null && avatar.IsAlive && avatar.GetComponent<PlayerKnockdown>()?.IsDown!=true &&
        ShoppingCart.For(avatar)==null && avatar.GetComponent<PlayerMonkeyCarry>()?.Held==null && avatar.GetComponent<PlayerMannequinCarry>()?.Held==null &&
        (network==null || !network.IsServerInitialized || NetworkLobby.Instance?.CanEditOutfit(network)==true) && NetworkLobby.Instance?.Results==null;
    private void Awake(){avatar=GetComponent<PlayerAvatar>();network=GetComponent<NetworkPlayer>();}
    public void Grant(){Apply(true,false);}
    public void Apply(bool owned,bool equipped){Owned=owned;Equipped=owned&&equipped;}
    public void Toggle()
    {
        if(!Owned || !CanUse)return;
        if(network!=null)network.RequestCameraEquip(!Equipped);else Apply(true,!Equipped);
    }
    public void Stow(){if(!Equipped)return;if(network!=null)network.StowCamera();else Apply(Owned,false);}
    public void Shoot()
    {
        if(!Equipped || CooldownRemaining>0)return;
        if(network!=null)network.RequestPhoto();else if(TryShoot()){ShowFlash();CapturePhoto();}
    }
    public bool TryShoot()
    {
        if(!Owned || !Equipped || !CanUse || Time.time<nextShot || !avatar.TryGetView(out var view))return false;
        nextShot=Time.time+Cooldown;Shots++;
        return true;
    }
    public void ShowFlash(){nextShot=Time.time+Cooldown;flashUntil=Time.time+.12f;}
    public void CapturePhoto(){if(avatar.IsLocal)StartCoroutine(CaptureAfterPose());}
    private IEnumerator CaptureAfterPose()
    {
        // Let the accepted flash animate on all observers; capture the world without the game HUD.
        yield return new WaitForEndOfFrame();
        if(avatar!=null && avatar.IsLocal && avatar.EyeCamera!=null)PhotoAlbum.Instance?.Capture(avatar.EyeCamera,model);
    }
    private void Update()
    {
        if(avatar==null)return;
        if(Equipped && !CanUse){if(network==null || network.IsOwner || network.IsServerInitialized)Stow();return;}
        if(!avatar.IsLocal || !Owned)return;
        if(HowToPlay.BlocksInput || PhotoAlbum.BlocksInput || PlayerChat.BlocksInput || EmoteWheel.BlocksInput || NetworkLobby.Instance!=null&&!NetworkLobby.Instance.InputAllowed)return;
        if(Keyboard.current?.kKey.wasPressedThisFrame==true)Toggle();
    }
    private void LateUpdate()
    {
        if(avatar==null)return;
        if(model==null && Equipped)BuildModel();
        if(model==null)return;
        model.gameObject.SetActive(Equipped && avatar.IsAlive);
        if(!Equipped || !avatar.TryGetView(out var view))return;
        float recoil=Mathf.Clamp01((flashUntil-Time.time)/.12f)*.035f;
        model.SetPositionAndRotation(view.Eye+view.Rotation*new Vector3(.23f,-.20f,.64f-recoil),view.Rotation);
        flash.enabled=Time.time<flashUntil;
    }
    private Material Material(Color colour){var prototype=Resources.Load<Material>("PhotoCameraHand");var mat=prototype!=null?new Material(prototype):new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=colour;return mat;}
    private void BuildModel()
    {
        shell=Material(new Color(.24f,.82f,.68f));dark=Material(new Color(.025f,.055f,.07f));accent=Material(new Color(1,.85f,.5f));
        model=PhotoCameraModel.Create(transform,shell,dark,accent).transform;model.localScale=Vector3.one*.75f;
        var lamp=new GameObject("Photo flash");lamp.transform.SetParent(model,false);lamp.transform.localPosition=new Vector3(.07f,.08f,.085f);
        flash=lamp.AddComponent<Light>();flash.type=LightType.Spot;flash.range=10;flash.spotAngle=70;flash.intensity=140;flash.enabled=false;
    }
    private void OnDestroy(){if(shell!=null)Destroy(shell);if(dark!=null)Destroy(dark);if(accent!=null)Destroy(accent);if(avatar!=null&&avatar.IsLocal)PhotoAlbum.Instance?.ResetRound();}
}
