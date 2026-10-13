using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local feedback only; damage is still decided by the mannequin on the host.</summary>
[DefaultExecutionOrder(320)]
public sealed class PlayerFearEffects : MonoBehaviour
{
    public const float DangerRadius = 5;
    private MannequinBrain[] mannequins;
    private Camera eye;
    private AudioSource heartbeat;
    private AudioClip pulse;
    private UnityEngine.UI.Image edge;
    private Texture2D edgeTexture;
    private Sprite edgeSprite;
    private GameObject overlay;
    private float danger, sampledDanger, nextSample, scareUntil;
    private Vector3 scareDirection;
    public float Danger => danger;
    public int Scares { get; private set; }
    private void Awake()
    {
        eye = GetComponent<Camera>();
        var audio = new GameObject("Heartbeat"); audio.transform.SetParent(transform, false);
        heartbeat = audio.AddComponent<AudioSource>(); heartbeat.playOnAwake = false; heartbeat.loop = true;
        heartbeat.spatialBlend = 0;
        const int rate = 22050; var samples = new float[rate];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float p = Mathf.Exp(-t * 24) + (t >= .23f ? .7f * Mathf.Exp(-(t - .23f) * 28) : 0);
            samples[i] = Mathf.Sin(t * 2 * Mathf.PI * 58) * p * .65f;
        }
        pulse = AudioClip.Create("Heartbeat pulse", samples.Length, 1, rate, false); pulse.SetData(samples, 0); heartbeat.clip = pulse;
        overlay = new GameObject("Mannequin danger", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        overlay.transform.SetParent(transform, false); var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
        var go = new GameObject("Danger edges", typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(overlay.transform, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        edge = go.GetComponent<UnityEngine.UI.Image>(); edge.raycastTarget = false;
        edgeTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float r = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
            edgeTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.58f, 1.35f, r))));
        }
        edgeTexture.Apply(); edgeSprite = Sprite.Create(edgeTexture, new Rect(0, 0, 64, 64), Vector2.one * .5f);
        edge.sprite = edgeSprite; edge.color = Color.clear;
    }
    private void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; Scan(); }
    private void SceneLoaded(Scene scene, LoadSceneMode mode) => Scan();
    private void Scan() => mannequins = FindObjectsByType<MannequinBrain>(FindObjectsInactive.Exclude);
    public void Hit(Vector3 direction)
    {
        Scares++; scareUntil = Time.unscaledTime + .48f; scareDirection = -direction.normalized;
        sampledDanger = 1;
    }
    private void LateUpdate()
    {
        var player = PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal && p.IsAlive);
        bool playing = player != null && player.gameObject.scene == gameObject.scene && NetworkLobby.Instance?.Results == null && NetworkLobby.Instance?.MenuVisible != true;
        if (Time.unscaledTime >= nextSample)
        {
            nextSample = Time.unscaledTime + .12f; sampledDanger = 0;
            if (playing && mannequins != null) foreach (var m in mannequins)
            {
                if (m == null || m.gameObject.scene != player.gameObject.scene || !m.gameObject.activeInHierarchy ||
                    m.GetComponent<NetworkMannequinAuthority>()?.RoundActive == false || m.GetComponent<MannequinStun>()?.IsStunned == true) continue;
                float distance = Vector3.Distance(player.Position, m.transform.position);
                sampledDanger = Mathf.Max(sampledDanger, 1 - Mathf.Clamp01(distance / DangerRadius));
            }
        }
        danger = Mathf.MoveTowards(danger, playing ? sampledDanger : 0, Time.unscaledDeltaTime * 2);
        heartbeat.volume = danger * .42f; heartbeat.pitch = Mathf.Lerp(.8f, 1.7f, danger);
        if (danger > .02f && !heartbeat.isPlaying) heartbeat.Play(); else if (danger <= .02f && heartbeat.isPlaying) heartbeat.Stop();
        float scare = Mathf.Clamp01((scareUntil - Time.unscaledTime) / .48f);
        edge.color = new Color(.45f, .015f, .025f, Mathf.Clamp01(danger * .48f + scare * .8f));
        if (playing || scare > 0)
        {
            float amount = danger * .18f + scare * 1.8f;
            transform.rotation *= Quaternion.Euler(Mathf.Sin(Time.unscaledTime * 29) * amount, 0, Mathf.Sin(Time.unscaledTime * 37) * amount);
            if (scare > 0 && scareDirection.sqrMagnitude > .1f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(scareDirection + Vector3.up * .12f), scare * .45f);
        }
    }
    private void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; heartbeat?.Stop(); if (edge != null) edge.color = Color.clear; }
    private void OnDestroy() { if (pulse != null) Destroy(pulse); if (edgeSprite != null) Destroy(edgeSprite); if (edgeTexture != null) Destroy(edgeTexture); }
}
