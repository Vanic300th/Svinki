using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Local microphone lifetime follows push-to-talk; speech is never recorded to disk.
[DefaultExecutionOrder(-150)]
public sealed class ProximityVoice : MonoBehaviour
{
    public static ProximityVoice Instance { get; private set; }
#if UNITY_EDITOR || DEBUG
    public bool TestCaptureSuppressed { get; set; }
#endif
    public bool IsTransmitting=>microphone!=null;
    public string Status { get; private set; }="HOLD B · VOICE · 25 m";
    private AudioClip microphone;private string device;private int cursor,stepIndex;private ushort sequence;
    private float lastAdvance,lastPump;private int lastPosition;private bool requesting;
    private float[] captured;private int captureFrames;
    private readonly float[] frame=new float[VoiceCodec.FrameSamples];
    private TMP_Text label;private NetworkPlayer speaker;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetStatics()=>Instance=null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap(){if(Instance==null)new GameObject("Proximity voice").AddComponent<ProximityVoice>();}
    private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}
    public static bool CanTransmit(NetworkPlayer player)
    {
        var lobby=NetworkLobby.Instance;
        return player!=null&&player.IsOwner&&player.IsClientStarted&&!player.IsDead&&lobby!=null&&!lobby.Offline&&
            lobby.Snapshot.phase==SessionPhase.Round&&lobby.Results==null&&!HowToPlay.BlocksInput && !PhotoAlbum.BlocksInput&&!lobby.IsSpectator&&!lobby.MenuVisible&&!PlayerChat.BlocksInput&&!EmoteWheel.BlocksInput;
    }
    private void Start()
    {
        var canvas=new GameObject("Voice HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));canvas.transform.SetParent(transform,false);
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=105;
        var scale=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
        var go=new GameObject("Push to talk status",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(canvas.transform,false);label=go.GetComponent<TextMeshProUGUI>();
        var rect=label.rectTransform;rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(24,26);rect.sizeDelta=new Vector2(620,36);
        label.font=PlayerChat.Instance.Font;label.fontSize=20;label.alignment=TextAlignmentOptions.MidlineLeft;label.richText=false;label.raycastTarget=false;
    }
    private void Update()
    {
        NetworkPlayer local=null;foreach(var avatar in PlayerRegistry.Players)if(avatar!=null&&avatar.IsLocal){local=avatar.GetComponent<NetworkPlayer>();break;}
        bool held=Keyboard.current?.bKey.isPressed==true&&CanTransmit(local)&&Application.isFocused;
#if UNITY_EDITOR || DEBUG
        held &= !TestCaptureSuppressed;
#endif
        if(speaker!=local){StopCapture();speaker=local;}
        if(!held){StopCapture();if(Keyboard.current?.bKey.wasReleasedThisFrame==true)Status="HOLD B · VOICE · 25 m";}
        else if(microphone==null&&!requesting&&Keyboard.current.bKey.wasPressedThisFrame)StartCoroutine(BeginCapture());
        if(held&&microphone!=null)Pump();
        bool visible=local!=null&&NetworkLobby.Instance!=null&&NetworkLobby.Instance.Results==null&&!NetworkLobby.Instance.MenuVisible&&!PlayerChat.IsOpen&&!EmoteWheel.IsOpen;
        if(label!=null){label.gameObject.SetActive(visible);label.text=local!=null&&local.IsDead?"VOICE MUTED · SPECTATING":Status;label.color=IsTransmitting?new Color(.4f,1,.73f):new Color(.76f,.85f,.89f);}
    }
    private IEnumerator BeginCapture()
    {
        requesting=true;
#if UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_EDITOR_OSX
        if(!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Status="ALLOW MICROPHONE ACCESS TO USE VOICE";
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone)){Status="MICROPHONE PERMISSION REQUIRED";requesting=false;yield break;}
        }
#endif
        requesting=false;
        if(!CanTransmit(speaker)||Keyboard.current?.bKey.isPressed!=true||!Application.isFocused)yield break;
        if(Microphone.devices.Length==0){Status="NO MICROPHONE FOUND";yield break;}
        device=Microphone.devices[0];
        Microphone.GetDeviceCaps(device,out int minRate,out int maxRate);
        int rate=VoiceCodec.SampleRate;if(minRate>0)rate=Mathf.Max(rate,minRate);if(maxRate>0)rate=Mathf.Min(rate,maxRate);
        try{microphone=Microphone.Start(device,true,1,rate);}
        catch(System.Exception){Status="MICROPHONE UNAVAILABLE";}
        if(microphone==null){Status="MICROPHONE UNAVAILABLE";yield break;}
        if(microphone.channels<1||microphone.channels>8||microphone.frequency<8000||microphone.frequency>96000){StopCapture();Status="MICROPHONE FORMAT UNAVAILABLE";yield break;}
        captureFrames=Mathf.RoundToInt(microphone.frequency*.02f);captured=new float[captureFrames*microphone.channels];
        cursor=stepIndex=lastPosition=0;lastAdvance=lastPump=Time.unscaledTime;Status="STARTING MICROPHONE…";
    }
    private void Pump()
    {
        int position=Microphone.GetPosition(device);
        if(position<0){StopCapture();Status="MICROPHONE DISCONNECTED";return;}
        if(position!=lastPosition){lastAdvance=Time.unscaledTime;lastPosition=position;}
        if(Time.unscaledTime-lastAdvance>2){StopCapture();Status="NO MICROPHONE INPUT";return;}
        if(Time.unscaledTime-lastPump>.4f){cursor=position;stepIndex=0;}lastPump=Time.unscaledTime;
        int available=(position-cursor+microphone.samples)%microphone.samples;
        // Keep latency bounded after a slow frame; at most five 20 ms frames per update.
        if(available>captureFrames*5){cursor=(position-captureFrames*5+microphone.samples)%microphone.samples;available=captureFrames*5;}
        int sent=0;
        while(available>=captureFrames&&sent++<5)
        {
            if(!microphone.GetData(captured,cursor)){StopCapture();Status="MICROPHONE READ FAILED";return;}
            VoiceCodec.DownmixResample(captured,microphone.channels,frame);
            speaker.SendVoice(sequence++,VoiceCodec.Encode(frame,ref stepIndex));cursor=(cursor+captureFrames)%microphone.samples;available-=captureFrames;
            Status="MIC LIVE · B TO TALK · 25 m";
        }
    }
    private void StopCapture()
    {
        if(microphone==null)return;
        Microphone.End(device);Destroy(microphone);microphone=null;cursor=stepIndex=0;
        if(Status.StartsWith("MIC LIVE")||Status.StartsWith("STARTING"))Status="HOLD B · VOICE · 25 m";
    }
    private void OnApplicationFocus(bool focus){if(!focus)StopCapture();}
    private void OnDisable()=>StopCapture();
    private void OnDestroy(){StopCapture();if(Instance==this)Instance=null;}
}
