using System.Collections.Generic;
using UnityEngine;

/// <summary>A single-use ragdoll chimp: one wrist is pinned to the carrier, impacts stay on the host.</summary>
[DefaultExecutionOrder(275),RequireComponent(typeof(ArticulatedRagdoll),typeof(CapsuleCollider))]
public sealed class MonkeyToy : MonoBehaviour
{
    public static readonly List<MonkeyToy> All=new List<MonkeyToy>();
    private readonly RaycastHit[] hits=new RaycastHit[48];
    private ArticulatedRagdoll ragdoll;private CapsuleCollider pickup;private NetworkMonkeyToy network;
    private PlayerAvatar holder;private bool physicsStarted,consumed;private float swingUntil,nextSwing;private Vector3 spawn;
    private Quaternion leftGripRotation=Quaternion.identity;
    private Vector3 gripPosition;
    private Quaternion gripRotation;
    private Transform gripVisual, gripPalm, gripForearm;
    private Material gripMaterial;
    public const float SwingDuration = .84f;
    public float SwingProgress => swingUntil > Time.time ? 1 - (swingUntil - Time.time) / SwingDuration : 0;
    public PlayerAvatar Holder=>holder;
    public bool IsHeld=>holder!=null;
    public bool Consumed=>consumed;
    public bool HasAuthority=>NetworkLobby.Instance==null||NetworkLobby.Instance.Offline||network==null||!network.enabled||network.NetworkObject!=null&&network.IsServerInitialized;
    private bool LocalSimulation=>HasAuthority||holder!=null&&holder.IsLocal;
    private void Awake(){ragdoll=GetComponent<ArticulatedRagdoll>();pickup=GetComponent<CapsuleCollider>();network=GetComponent<NetworkMonkeyToy>();spawn=transform.position;}
    private void OnEnable(){if(!All.Contains(this))All.Add(this);}
    private void OnDisable(){if(holder!=null)holder.GetComponent<PlayerMonkeyCarry>()?.Clear(this);All.Remove(this);if(gripVisual!=null)gripVisual.gameObject.SetActive(false);}
    private void Start(){if(HasAuthority&&!physicsStarted)BeginPhysics();}
    public void BeginPhysics(){if(physicsStarted)return;physicsStarted=true;ragdoll.Begin(HasAuthority,HasAuthority,Vector3.zero);CalmPhysics();}
    private void CalmPhysics()
    {
        // Plush joints have a small, damped range rather than loose mannequin limbs.
        ragdoll.SetDamping(IsHeld?3:1.5f,IsHeld?8:4);
        ragdoll.SetJointMotion(35,50,35,4);
    }
    public bool TryGrab(PlayerAvatar player)
    {
        if(!HasAuthority||consumed||IsHeld||player==null||!player.IsAlive||player.gameObject.scene!=gameObject.scene||
            player.GetComponent<PlayerKnockdown>()?.IsDown==true||NetworkLobby.Instance?.Results!=null||ShoppingCart.For(player)!=null||
            player.GetComponent<PlayerMannequinCarry>()?.Held!=null||player.GetComponent<PlayerMonkeyCarry>()?.Held!=null)return false;
        var netPlayer=player.GetComponent<NetworkPlayer>();if(netPlayer!=null&&NetworkLobby.Instance?.CanEditOutfit(netPlayer)!=true)return false;
        var direction=pickup.ClosestPoint(player.EyePosition)-player.EyePosition;
        if(direction.magnitude>3.8f)return false;
        if(direction.magnitude>.01f&&gameObject.scene.GetPhysicsScene().Raycast(player.EyePosition,direction.normalized,out var obstruction,direction.magnitude+.05f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!Owns(obstruction.collider))return false;
        ApplyHolder(player);network?.SetCarrier(player);return true;
    }
    public void ApplyHolder(PlayerAvatar player)
    {
        if(holder==player)return;
        if(holder!=null)holder.GetComponent<PlayerMonkeyCarry>()?.Clear(this);
        holder=player;pickup.enabled=holder==null;swingUntil=0;nextSwing=0;
        if(holder!=null)
        {
            holder.GetComponent<PlayerMonkeyCarry>()?.Set(this);
            if(holder.TryGetView(out var view))
            {
                ragdoll.End();transform.SetPositionAndRotation(view.Eye+view.Rotation*new Vector3(.30f,0,.95f),view.Rotation*Quaternion.Euler(0,180,0));
                var bones=GetComponentInChildren<SkinnedMeshRenderer>().bones;
                Transform left=null;
                foreach(var bone in bones){if(bone.name=="Hand_L")left=bone;}
                if(left!=null)
                {
                    transform.position+=view.Eye+view.Rotation*new Vector3(.24f,.28f,1.32f)-left.position;
                    leftGripRotation=Quaternion.Inverse(view.Rotation)*left.rotation;
                }
            }
        }
        ragdoll.Begin(LocalSimulation,HasAuthority,Vector3.zero);physicsStarted=true;CalmPhysics();PinHands(true);
    }
    public bool Release()
    {
        if(!HasAuthority||holder==null)return false;ApplyHolder(null);network?.SetCarrier(null);return true;
    }
    public bool Swing()
    {
        if(!HasAuthority||holder==null||consumed||!holder.IsAlive||holder.GetComponent<PlayerKnockdown>()?.IsDown==true||
            NetworkLobby.Instance?.Results!=null||Time.time<nextSwing)return false;
        AnimateSwing();network?.SetSwing();return true;
    }
    public void AnimateSwing(){swingUntil=Time.time+SwingDuration;nextSwing=Time.time+1.02f;}
    private static Vector3 SwingOffset(float t)
    {
        var idle=new Vector3(.24f,.28f,1.32f);var windup=new Vector3(.42f,.35f,1.03f);var strike=new Vector3(.10f,-.12f,1.62f);
        if(t<.28f)return Vector3.Lerp(idle,windup,Mathf.SmoothStep(0,1,t/.28f));
        if(t<.58f)return Vector3.Lerp(windup,strike,Mathf.SmoothStep(0,1,(t-.28f)/.30f));
        return Vector3.Lerp(strike,idle,Mathf.SmoothStep(0,1,(t-.58f)/.42f));
    }
    private void PinHands(bool immediate)
    {
        if(holder==null||!holder.TryGetView(out var view))return;
        float t=SwingProgress;bool swinging=swingUntil>Time.time;
        Vector3 target=view.Eye+view.Rotation*(swinging?SwingOffset(t):new Vector3(.24f,.28f,1.32f));
        Quaternion rotation=view.Rotation*leftGripRotation*Quaternion.Euler(0,0,swinging?-Mathf.Sin(t*Mathf.PI)*35:0);
        float blend=1-Mathf.Exp(-Time.fixedDeltaTime*10);
        if(immediate||(target-gripPosition).sqrMagnitude>4){gripPosition=target;gripRotation=rotation;}
        else{gripPosition=Vector3.Lerp(gripPosition,target,blend);gripRotation=Quaternion.Slerp(gripRotation,rotation,blend);}
        // Only this wrist is fixed: the free arm and the rest of the plush swing under gravity.
        ragdoll.Pin("Hand_L",gripPosition,gripRotation,immediate);
    }
    private void UpdateGripVisual()
    {
        bool show=holder!=null&&holder.IsLocal&&holder.TryGetView(out _);
        if(gripVisual!=null)gripVisual.gameObject.SetActive(show);
        if(!show)return;
        holder.TryGetView(out var view);
        if(gripVisual==null)
        {
            gripVisual=new GameObject("First person pig toy grip").transform;gripVisual.SetParent(transform,false);
            var skin=holder.GetComponentInChildren<PigAppearance>();
            var shader=GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial.shader;gripMaterial=new Material(shader);
            var color=PigAppearance.SkinColors[PigFace.Decode(skin!=null?skin.FaceCode:PigFace.Selected).skinColor];
            gripMaterial.SetColor("_BaseColor",color);gripMaterial.SetFloat("_VividGain",1);gripMaterial.SetFloat("_Saturation",1);gripMaterial.SetFloat("_Contrast",1);
            gripPalm=CreateGripPart("Pig paw",gripVisual);gripForearm=CreateGripPart("Pig forearm",gripVisual);
        }
        Vector3 wrist=ragdoll.BonePosition("Hand_L");
        Vector3 elbow=view.Eye+view.Rotation*new Vector3(.48f,-.35f,.25f);
        gripPalm.SetPositionAndRotation(wrist+view.Rotation*new Vector3(.012f,.004f,-.035f),view.Rotation*Quaternion.Euler(90,0,0));
        gripPalm.localScale=new Vector3(.095f,.065f,.078f);
        var delta=wrist-elbow;gripForearm.SetPositionAndRotation((wrist+elbow)*.5f,Quaternion.FromToRotation(Vector3.up,delta.normalized));
        gripForearm.localScale=new Vector3(.075f,delta.magnitude*.5f,.075f);
    }
    private Transform CreateGripPart(string name,Transform parent)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name=name;go.transform.SetParent(parent,false);
        var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=gripMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }
    private void OnDestroy(){if(gripMaterial!=null)Destroy(gripMaterial);}
    private void FixedUpdate()
    {
        if(consumed||!physicsStarted)return;
        if(holder!=null)
        {
            if(HasAuthority&&(!holder.isActiveAndEnabled||!holder.IsAlive||holder.GetComponent<PlayerKnockdown>()?.IsDown==true)){Release();return;}
            PinHands(false);
            if(HasAuthority&&Time.time<swingUntil&&SwingProgress>=.32f&&SwingProgress<=.68f)CheckStrike();
        }
    }
    private void LateUpdate()
    {
        UpdateGripVisual();
        if(!physicsStarted||consumed)return;
        if(!IsHeld&&HasAuthority)
        {
            var center=ragdoll.Center;transform.position=center;
            if(center.y < -8){ragdoll.End();transform.position=spawn;ragdoll.Begin(true,true,Vector3.zero);CalmPhysics();}
        }
    }
    private bool Owns(Collider collider)=>collider!=null&&(collider.transform.IsChildOf(transform)||collider.GetComponentInParent<RagdollColliderOwner>()?.Owner==gameObject);
    private void CheckStrike()
    {
        if(!holder.TryGetView(out var view))return;
        Vector3 direction=view.Rotation*Vector3.forward;Vector3 origin=view.Eye+view.Rotation*new Vector3(0,-.28f,.25f);
        int count=gameObject.scene.GetPhysicsScene().SphereCast(origin,.23f,direction,hits,1.65f,~0,QueryTriggerInteraction.Ignore);
        float wall=float.PositiveInfinity,nearest=float.PositiveInfinity;GameObject target=null;
        for(int i=0;i<count;i++)
        {
            var collider=hits[i].collider;if(Owns(collider)||collider.GetComponentInParent<PlayerAvatar>()!=null)continue;
            var owner=collider.GetComponentInParent<RagdollColliderOwner>()?.Owner;
            var candidate=owner!=null?owner:collider.gameObject;
            var brain=candidate.GetComponentInParent<MannequinBrain>();var display=candidate.GetComponentInParent<ThrowableMannequin>();var animated=candidate.GetComponentInParent<MannequinAnimator>();
            if(brain==null&&display==null&&animated==null){wall=Mathf.Min(wall,hits[i].distance);continue;}
            if(hits[i].distance<nearest){nearest=hits[i].distance;target=brain!=null?brain.gameObject:display!=null?display.gameObject:animated.gameObject;}
        }
        if(target==null||nearest>wall+.02f)return;
        // Consume before applying damage, so one swing can never hit two mannequins.
        consumed=true;
        try{target.GetComponent<MannequinStun>()?.TryStun(direction);}
        finally
        {
            holder.GetComponent<PlayerMonkeyCarry>()?.Clear(this);holder=null;
            if(network!=null&&network.NetworkObject!=null&&network.IsServerInitialized)network.NetworkManager.ServerManager.Despawn(network.NetworkObject);
            else Destroy(gameObject);
        }
    }
    public RagdollFrame Capture()=>ragdoll.Capture();
    public void Receive(RagdollFrame frame)=>ragdoll.Receive(frame);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>All.Clear();
}
