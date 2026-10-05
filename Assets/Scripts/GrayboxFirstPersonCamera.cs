using UnityEngine;
using UnityEngine.InputSystem;

public class GrayboxFirstPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float eyeHeight = 1.65f;
    [SerializeField] private float crouchEyeHeight = 0.95f;
    [SerializeField] private float mouseSensitivity = 0.12f;

    private float yaw;
    private float pitch;
    private GrayboxPlayerController playerController;

    private void Awake()
    {
        // Своя 3D-модель остаётся видимой зеркалу, но не закрывает обзор от первого лица.
        int mirrorLayer = LayerMask.NameToLayer("LocalPlayerMirror");
        Camera view = GetComponent<Camera>();
        if (view != null && mirrorLayer >= 0)
            view.cullingMask &= ~(1 << mirrorLayer);
    }

    private void Start()
    {
        if (target != null)
            yaw = target.eulerAngles.y;
        playerController = target != null ? target.GetComponent<GrayboxPlayerController>() : null;
        if (target != null) LockCursor();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if ((NetworkLobby.Instance == null || NetworkLobby.Instance.InputAllowed) && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -200f, 200f);
        }

        if (target.GetComponent<NetworkPlayer>() == null)
            target.rotation = Quaternion.Euler(0f, yaw, 0f);
        float crouchAmount = playerController != null ? playerController.CrouchAmount : 0f;
        transform.position = target.position + Vector3.up * Mathf.Lerp(eyeHeight, crouchEyeHeight, crouchAmount);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public void SetSpectatorMode() { target = null; playerController = null; }

    public void SetTarget(Transform value)
    {
        target = value;
        playerController = target != null ? target.GetComponent<GrayboxPlayerController>() : null;
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            LockCursor();
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
