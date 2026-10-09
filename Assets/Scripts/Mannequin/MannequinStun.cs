using UnityEngine;
using UnityEngine.AI;

/// <summary>Only the host accepts toy hits and runs the 30-second incapacitation timer.</summary>
[RequireComponent(typeof(MannequinBrain),typeof(ArticulatedRagdoll))]
public sealed class MannequinStun : MonoBehaviour
{
    public const float Duration=30f;
    public struct StunState { public bool Down; public uint Id; public Vector3 Direction; }
    private NetworkMannequinStun network;
    private ArticulatedRagdoll ragdoll;private MannequinBrain brain;private MannequinAnimator motion;private Animator animator;private NavMeshAgent agent;
    private Collider[] rootColliders;private bool[] colliderStates,skinStates;private SkinnedMeshRenderer[] skins;private bool brainWasEnabled,agentWasEnabled,motionWasEnabled,animatorWasEnabled;private float until;private uint fallId;
    public uint FallId => fallId;
    public bool IsStunned { get; private set; }
    public float Remaining => IsStunned?Mathf.Max(0,until-Time.time):0;
    private bool Authority => NetworkLobby.Instance==null||NetworkLobby.Instance.Offline||network==null||network.ServerActive;
    private void Awake()
    {ragdoll=GetComponent<ArticulatedRagdoll>();brain=GetComponent<MannequinBrain>();motion=GetComponent<MannequinAnimator>();animator=GetComponentInChildren<Animator>();agent=GetComponent<NavMeshAgent>();rootColliders=GetComponents<Collider>();colliderStates=new bool[rootColliders.Length];skins=GetComponentsInChildren<SkinnedMeshRenderer>(true);skinStates=new bool[skins.Length];network=GetComponent<NetworkMannequinStun>();}
    public bool TryStun(Vector3 direction)
    {
        if(!Authority||IsStunned||NetworkLobby.Instance?.Results!=null)return false;
        var value=new StunState{Down=true,Id=++fallId,Direction=direction.normalized};Apply(value);network?.SetState(value);return true;
    }
    public void Apply(StunState value)
    {
        if(value.Down==IsStunned)return;IsStunned=value.Down;
        if(value.Down)
        {
            fallId=value.Id;until=Time.time+Duration;brainWasEnabled=brain.enabled;agentWasEnabled=agent.enabled;motionWasEnabled=motion.enabled;animatorWasEnabled=animator.enabled;
            if(agent.enabled&&agent.isOnNavMesh)agent.ResetPath();brain.enabled=false;agent.enabled=false;motion.enabled=false;animator.enabled=false;
            for(int i=0;i<rootColliders.Length;i++){colliderStates[i]=rootColliders[i].enabled;rootColliders[i].enabled=false;}
            for(int i=0;i<skins.Length;i++){skinStates[i]=skins[i].updateWhenOffscreen;skins[i].updateWhenOffscreen=true;}
            ragdoll.Begin(Authority,Authority,value.Direction*2+Vector3.up*.15f);
        }
        else
        {
            ragdoll.End();for(int i=0;i<rootColliders.Length;i++)rootColliders[i].enabled=colliderStates[i];
            for(int i=0;i<skins.Length;i++)skins[i].updateWhenOffscreen=skinStates[i];
            animator.enabled=animatorWasEnabled;motion.enabled=motionWasEnabled;
            if(Authority){agent.enabled=agentWasEnabled;if(agent.enabled&&agent.isOnNavMesh)agent.Warp(transform.position);brain.enabled=brainWasEnabled;brain.ResumeAfterStun();}
        }
    }
    private void Update()
    {
        if(!Authority||!IsStunned)return;
        if(Time.time>=until){var value=new StunState{Id=fallId};Apply(value);network?.SetState(value);return;}
    }
    public RagdollFrame Capture()=>ragdoll.Capture(fallId);
    public void Receive(RagdollFrame frame){if(!Authority&&IsStunned&&frame.FallId==fallId)ragdoll.Receive(frame);}
}
