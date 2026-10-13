using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(310)]
public sealed class SpectatorCamera : MonoBehaviour
{
    private NetworkLobby lobby;
    private bool flying;
    private Vector3 position;
    private float yaw, pitch, nextSend;
    private StoreMap map;
    public static NetworkPlayer CurrentTarget => null;
    public Vector3 GhostPosition => position;
    private void Awake() => lobby = GetComponent<NetworkLobby>();
    private void LateUpdate()
    {
        if (!lobby.IsSpectator || lobby.Snapshot.phase != SessionPhase.Round)
        {
            if (flying)
            {
                flying = false; map = null;
                var local = PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal && p.IsAlive);
                if (local != null) Camera.main?.GetComponent<GrayboxFirstPersonCamera>()?.SetTarget(local.transform);
            }
            return;
        }
        var camera = Camera.main; if (camera == null) return;
        if (!flying)
        {
            flying = true; position = camera.transform.position + Vector3.up * 1.5f;
            yaw = camera.transform.eulerAngles.y; pitch = Mathf.DeltaAngle(0, camera.transform.eulerAngles.x);
            map = FindObjectsByType<StoreMap>().FirstOrDefault(m => m.gameObject.scene == camera.gameObject.scene);
            camera.GetComponent<GrayboxFirstPersonCamera>()?.SetSpectatorMode();
            var pickup = camera.GetComponent<PlayerPickupInteractor>(); if (pickup != null) pickup.enabled = false;
        }
        bool allowed = !HowToPlay.BlocksInput && !PhotoAlbum.BlocksInput && !lobby.MenuVisible && !lobby.CargoOpen;
        if (allowed)
        {
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            if (Mouse.current != null)
            {
                var mouse = Mouse.current.delta.ReadValue();
                float sensitivity = camera.GetComponent<GrayboxFirstPersonCamera>()?.MouseSensitivity ?? .12f;
                yaw += mouse.x * sensitivity; pitch = Mathf.Clamp(pitch - mouse.y * sensitivity, -85, 85);
            }
            var keys = Keyboard.current;
            var input = GrayboxPlayerController.ReadMoveInput();
            var motion = Quaternion.Euler(pitch, yaw, 0) * new Vector3(input.x, 0, input.y);
            if (keys?.spaceKey.isPressed == true) motion.y += 1;
            if (keys?.leftCtrlKey.isPressed == true || keys?.rightCtrlKey.isPressed == true) motion.y -= 1;
            position += Vector3.ClampMagnitude(motion, 1) * (keys?.leftShiftKey.isPressed == true ? 10 : 6) * Time.unscaledDeltaTime;
        }
        position = ClampToMap(position, map);
        camera.transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0));
        camera.GetComponentInChildren<FlashlightController>()?.SetOn(false);
        if (Time.unscaledTime >= nextSend)
        {
            nextSend = Time.unscaledTime + .05f;
            PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal)?.GetComponent<NetworkPlayer>()?.RequestGhostPose(position, yaw, pitch);
        }
    }
    public static Vector3 ClampToMap(Vector3 value, StoreMap map)
    {
        if (map != null)
        {
            value.x = Mathf.Clamp(value.x, map.Minimum.x + .15f, map.Maximum.x - .15f);
            value.z = Mathf.Clamp(value.z, map.Minimum.y + .15f, map.Maximum.y - .15f);
        }
        return value;
    }
}
