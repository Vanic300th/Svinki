using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FlashlightController : MonoBehaviour
{
    [SerializeField] private bool startOn = true;

    private Light flashlight;
    private bool isOn;
    public bool IsOn => isOn;
    public void SetOn(bool value) { isOn = value; if (flashlight != null) flashlight.enabled = value; }

    private void Awake()
    {
        flashlight = GetComponent<Light>() ?? GetComponentInChildren<Light>();
        isOn = startOn;
        if (flashlight != null) flashlight.enabled = isOn;
    }

    private void Update()
    {
        if (NetworkLobby.Instance != null && !NetworkLobby.Instance.InputAllowed) return;
        if (Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame) return;

        isOn = !isOn;
        if (flashlight != null) flashlight.enabled = isOn;
    }

    private void OnGUI()
    {
        if (NetworkLobby.Instance != null && (!NetworkLobby.Instance.InputAllowed || NetworkLobby.Instance.MenuVisible)) return;
        GUI.Box(new Rect(12f, 12f, 185f, 28f),
            $"Фонарик [F]: {(isOn ? "вкл." : "выкл.")}");
    }
}
