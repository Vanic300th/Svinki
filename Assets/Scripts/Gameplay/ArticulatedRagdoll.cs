using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Unscaled physics proxies drive the actual skinned bones, including pinned toy wrists.</summary>
[DefaultExecutionOrder(280)]
public sealed class ArticulatedRagdoll : MonoBehaviour
{
    [Serializable] public struct Segment
    {
        public Transform bone, end;
        public int parent;
        public float radius, mass;
        public Vector3 boxSize, boxCenter;
    }
    [SerializeField] private Segment[] segments = Array.Empty<Segment>();
    private readonly List<Rigidbody> bodies = new List<Rigidbody>();
    private readonly List<Collider> colliders = new List<Collider>();
    private GameObject physicsRoot;
    private Pose[] targets, rest;
    private bool running, simulate, received;
    public int BodyCount => bodies.Count;
    public bool Simulating => simulate;
    public Vector3 Center => bodies.Count > 0 ? bodies[0].position : transform.position;
    public Vector3 BonePosition(string name)
    { for(int i=0;i<segments.Length;i++) if(segments[i].bone.name==name)return bodies.Count>i?bodies[i].position:segments[i].bone.position; return transform.position; }
    private void Build()
    {
        if(physicsRoot!=null)return;
        physicsRoot=new GameObject(name+" articulated physics");SceneManager.MoveGameObjectToScene(physicsRoot,gameObject.scene);
        physicsRoot.AddComponent<RagdollColliderOwner>().Owner=gameObject;
        targets=new Pose[segments.Length];rest=new Pose[segments.Length];
        for(int i=0;i<segments.Length;i++)
        {
            var s=segments[i]; rest[i]=new Pose(s.bone.localPosition,s.bone.localRotation);
            var proxy=new GameObject(s.bone.name+" physics");proxy.transform.SetParent(physicsRoot.transform);proxy.transform.SetPositionAndRotation(s.bone.position,s.bone.rotation);
            var body=proxy.AddComponent<Rigidbody>();body.isKinematic=true;body.mass=Mathf.Max(.05f,s.mass);body.linearDamping=.45f;body.angularDamping=.65f;
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations=14;body.solverVelocityIterations=8;body.maxDepenetrationVelocity=2;body.maxAngularVelocity=12;
            Collider collider;
            if(s.end!=null)
            {
                var shape=new GameObject("Limb collider");shape.transform.SetParent(proxy.transform,false);
                var delta=s.end.position-s.bone.position;shape.transform.position=s.bone.position+delta*.5f;shape.transform.rotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
                var capsule=shape.AddComponent<CapsuleCollider>();capsule.radius=s.radius;capsule.height=Mathf.Max(s.radius*2,delta.magnitude);collider=capsule;
            }
            else {var box=proxy.AddComponent<BoxCollider>();box.size=s.boxSize;box.center=s.boxCenter;collider=box;}
            collider.enabled=false;bodies.Add(body);colliders.Add(collider);
            if(s.parent>=0)
            {
                var joint=proxy.AddComponent<CharacterJoint>();joint.connectedBody=bodies[s.parent];joint.autoConfigureConnectedAnchor=false;
                joint.connectedAnchor=bodies[s.parent].transform.InverseTransformPoint(s.bone.position);
                joint.lowTwistLimit=new SoftJointLimit{limit=-60};joint.highTwistLimit=new SoftJointLimit{limit=60};
                joint.swing1Limit=new SoftJointLimit{limit=80};joint.swing2Limit=new SoftJointLimit{limit=60};
                joint.enableProjection=true;joint.projectionDistance=.06f;joint.projectionAngle=15;
            }
        }
    }
    public void Begin(bool authority, bool worldCollisions, Vector3 impulse)
    {
        Build();running=true;simulate=authority;received=false;
        for(int i=0;i<bodies.Count;i++)
        {
            bodies[i].position=segments[i].bone.position;bodies[i].rotation=segments[i].bone.rotation;
            bodies[i].isKinematic=!authority;colliders[i].enabled=authority&&worldCollisions;
            if(authority){bodies[i].linearVelocity=impulse;bodies[i].angularVelocity=Vector3.zero;bodies[i].WakeUp();}
        }
        if(authority&&worldCollisions)
        {
            for(int i=0;i<colliders.Count;i++)for(int j=i+1;j<colliders.Count;j++)Physics.IgnoreCollision(colliders[i],colliders[j],true);
            foreach(var c in GetComponentsInChildren<Collider>(true))foreach(var part in colliders)Physics.IgnoreCollision(c,part,true);
        }
    }
    public void SetDamping(float linear,float angular)
    {foreach(var body in bodies){body.linearDamping=linear;body.angularDamping=angular;}}
    public void Pin(string name,Vector3 point,Quaternion rotation,bool immediate)
    {
        if(!simulate)return;
        for(int i=0;i<segments.Length;i++)if(segments[i].bone.name==name)
        {
            var body=bodies[i];body.isKinematic=true;
            if(immediate){body.position=point;body.rotation=rotation;}else{body.MovePosition(point);body.MoveRotation(rotation);}return;
        }
    }
    public void Unpin()
    {if(!simulate)return;foreach(var body in bodies){body.isKinematic=false;body.WakeUp();}}
    public void End()
    {
        if(!running)return;running=false;
        for(int i=0;i<bodies.Count;i++){bodies[i].isKinematic=true;colliders[i].enabled=false;segments[i].bone.SetLocalPositionAndRotation(rest[i].position,rest[i].rotation);}
    }
    public RagdollFrame Capture(uint id=0)
    {var frame=new RagdollFrame{FallId=id,Positions=new Vector3[bodies.Count],Rotations=new Quaternion[bodies.Count]};for(int i=0;i<bodies.Count;i++){frame.Positions[i]=bodies[i].position;frame.Rotations[i]=bodies[i].rotation;}return frame;}
    public void Receive(RagdollFrame frame)
    {
        if(!running||simulate||frame.Positions==null||frame.Rotations==null||frame.Positions.Length!=bodies.Count||frame.Rotations.Length!=bodies.Count)return;
        for(int i=0;i<bodies.Count;i++){targets[i]=new Pose(frame.Positions[i],frame.Rotations[i]);if(!received)bodies[i].transform.SetPositionAndRotation(targets[i].position,targets[i].rotation);}received=true;
    }
    private void LateUpdate()
    {
        if(!running)return;float blend=1-Mathf.Exp(-Time.deltaTime*30);
        for(int i=0;i<bodies.Count;i++)
        {
            var p=bodies[i].transform;if(!simulate&&received)p.SetPositionAndRotation(Vector3.Lerp(p.position,targets[i].position,blend),Quaternion.Slerp(p.rotation,targets[i].rotation,blend));
            segments[i].bone.SetPositionAndRotation(p.position,p.rotation);
        }
    }
    private void OnDisable(){foreach(var b in bodies)if(b!=null)b.isKinematic=true;foreach(var c in colliders)if(c!=null)c.enabled=false;}
    private void OnDestroy(){if(physicsRoot!=null)Destroy(physicsRoot);}
}
