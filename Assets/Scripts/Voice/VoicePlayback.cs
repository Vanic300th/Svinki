using UnityEngine;

[DisallowMultipleComponent]
public sealed class VoicePlayback : MonoBehaviour
{
    public const float NearDistance=2, FarDistance=25;
    private const int Capacity=VoiceCodec.SampleRate/2, Prebuffer=VoiceCodec.FrameSamples*4;
    private readonly object sync=new object();
    private readonly float[] ring=new float[Capacity],decoded=new float[VoiceCodec.FrameSamples];
    private AudioSource source;private AudioClip clip;private PlayerAvatar avatar;
    private int read,count;private bool primed,received;private ushort lastSequence;
    private float lastPacket=-10,lastAudible=-10,lastSample;
    public bool IsSpeaking=>Time.unscaledTime-lastAudible<.25f&&avatar!=null&&avatar.IsAlive;
    public int ReceivedFrames { get; private set; }
    public int BufferedSamples { get {lock(sync)return count;} }
    public static float GainAtDistance(float distance)=>Mathf.Clamp01((FarDistance-Mathf.Max(NearDistance,distance))/(FarDistance-NearDistance));
    private void Awake()
    {
        avatar=GetComponent<PlayerAvatar>();
        var go=new GameObject("Positional player voice");go.transform.SetParent(transform,false);source=go.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=true;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;
        source.minDistance=NearDistance;source.maxDistance=FarDistance;source.dopplerLevel=0;source.volume=1;source.priority=32;
        clip=AudioClip.Create("Live player voice",VoiceCodec.SampleRate,1,VoiceCodec.SampleRate,true,ReadAudio);source.clip=clip;
    }
    public void Receive(ushort sequence,byte[] packet)
    {
        if(!VoiceCodec.Decode(packet,decoded)||avatar==null||!avatar.IsAlive)return;
        bool fresh=Time.unscaledTime-lastPacket>.5f;
        if(received&&!fresh&&!VoiceCodec.IsNewer(sequence,lastSequence))return;
        int missing=received&&!fresh?Mathf.Clamp((ushort)(sequence-lastSequence)-1,0,3):0;
        lock(sync)
        {
            if(fresh){read=count=0;primed=false;lastSample=0;}
            // Drop old queued audio rather than accumulate seconds of delayed conversation.
            if(count+VoiceCodec.FrameSamples*(missing+1)>Capacity){read=count=0;primed=false;}
            for(int i=0;i<missing*VoiceCodec.FrameSamples;i++)Append(0);
            for(int i=0;i<decoded.Length;i++)Append(decoded[i]);
        }
        float energy=0;foreach(float sample in decoded)energy+=sample*sample;
        if(energy/decoded.Length>.00001f)lastAudible=Time.unscaledTime;
        lastPacket=Time.unscaledTime;lastSequence=sequence;received=true;ReceivedFrames++;
        if(!source.isPlaying)source.Play();
    }
    private void Append(float sample){ring[(read+count)%Capacity]=sample;count++;}
    private void ReadAudio(float[] data)
    {
        lock(sync)
        {
            if(!primed&&count>=Prebuffer)primed=true;
            for(int i=0;i<data.Length;i++)
            {
                if(primed&&count>0){lastSample=ring[read];read=(read+1)%Capacity;count--;}
                else lastSample*=.9f;
                data[i]=lastSample;
            }
            if(count==0)primed=false;
        }
    }
    private void LateUpdate()
    {
        if(source==null)return;
        source.transform.position=avatar!=null?avatar.EyePosition:transform.position+Vector3.up*1.6f;
        source.mute=avatar==null||!avatar.IsAlive||NetworkLobby.Instance==null||NetworkLobby.Instance.Results!=null||NetworkLobby.Instance.Snapshot.phase!=SessionPhase.Round;
        if(Time.unscaledTime-lastPacket>.5f&&source.isPlaying){source.Stop();lock(sync){read=count=0;primed=false;lastSample=0;}}
    }
    private void OnDestroy(){if(source!=null)source.Stop();if(clip!=null)Destroy(clip);}
}
