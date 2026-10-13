using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Starts the scene's background track after loading and recovers interrupted playback.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
public sealed class RoundBackgroundMusic : MonoBehaviour
{
    [SerializeField] private LobbyMediaPlayer[] suppressWhilePlaying = new LobbyMediaPlayer[0];
    [SerializeField, Min(.05f)] private float fadeSeconds = 1;
    private AudioSource source;
    private float nextCheck;
    private AudioClip failedClip;
    private float backgroundVolume;
    private bool mediaPlaying, pausedForMedia;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Subscribe()
    {
        SceneManager.sceneLoaded -= BindScene;
        SceneManager.sceneLoaded += BindScene;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BindInitialScenes()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)BindScene(SceneManager.GetSceneAt(i),LoadSceneMode.Additive);
    }

    private static void BindScene(Scene scene,LoadSceneMode mode)
    {
        foreach(var root in scene.GetRootGameObjects())
            foreach(var audio in root.GetComponentsInChildren<AudioSource>(true))
                if(audio.name=="Ambient Music" && audio.GetComponent<RoundBackgroundMusic>()==null)
                    audio.gameObject.AddComponent<RoundBackgroundMusic>();
    }

    private void OnEnable()
    {
        Subscribe();source=GetComponent<AudioSource>();nextCheck=0;failedClip=null;
        backgroundVolume=source.volume;mediaPlaying=false;pausedForMedia=false;
    }
    private void OnDisable()
    {
        if(source==null || suppressWhilePlaying.Length==0)return;
        source.volume=backgroundVolume;
        if(pausedForMedia)source.UnPause();
        pausedForMedia=false;
    }
    private void Update()
    {
        bool check=Time.unscaledTime>=nextCheck;
        if(check)
        {
            nextCheck=Time.unscaledTime+.5f;
            mediaPlaying=false;
            // Read the already replicated playback state locally; no extra network messages.
            foreach(var media in suppressWhilePlaying)
                if(media!=null && media.isActiveAndEnabled && media.State.Playing && !string.IsNullOrEmpty(media.State.Url))
                {mediaPlaying=true;break;}
        }
        if(source!=null)
        {
            source.volume=Mathf.MoveTowards(source.volume,mediaPlaying?0:backgroundVolume * MusicSettings.Volume,
                Time.unscaledDeltaTime*backgroundVolume/fadeSeconds);
            if(mediaPlaying && source.volume<=0)
            {
                if(source.isPlaying){source.Pause();pausedForMedia=true;}
                return;
            }
            if(!mediaPlaying && pausedForMedia){source.UnPause();pausedForMedia=false;}
        }
        if(!check)return;
        if(source==null || !source.enabled || source.clip==null || source.mute || source.volume<=0 ||
            AudioListener.pause && !source.ignoreListenerPause)return;
        var clip=source.clip;
        if(clip.loadState==AudioDataLoadState.Failed)
        {
            if(failedClip!=clip){failedClip=clip;Debug.LogWarning("Background music could not load: "+clip.name,this);}
            return;
        }
        if(clip.loadState==AudioDataLoadState.Unloaded){clip.LoadAudioData();return;}
        if(clip.loadState!=AudioDataLoadState.Loaded || source.isPlaying)return;
        source.loop=true;
        source.spatialBlend=0;
        source.Play();
    }
}
