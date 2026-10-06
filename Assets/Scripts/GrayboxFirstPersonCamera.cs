using UnityEngine;
using UnityEngine.InputSystem;

public class GrayboxFirstPersonCamera : MonoBehaviour
{
    public const float EyeForwardOffset = 0.22f;
    [SerializeField] private Transform target;
    [SerializeField] private float eyeHeight = 1.65f;
    [SerializeField] private float crouchEyeHeight = 0.95f;
    [SerializeField] private float mouseSensitivity = 0.12f;

    public float MouseSensitivity => mouseSensitivity * MouseSettings.Multiplier;
    private float yaw;
    private float pitch;
    private GrayboxPlayerController playerController;
    private PlayerKnockdown knockdown;

    private void Awake()
    {
        // Своя 3D-модель остаётся видимой зеркалу, но не закрывает обзор от первого лица.
        int mirrorLayer = LayerMask.NameToLayer("LocalPlayerMirror");
        Camera view = GetComponent<Camera>();
        if (view != null) view.nearClipPlane = Mathf.Min(view.nearClipPlane, 0.05f);
        if (view != null && mirrorLayer >= 0)
            view.cullingMask &= ~(1 << mirrorLayer);
    }

    private void Start()
    {
        if (target != null)
            yaw = target.eulerAngles.y;
        playerController = target != null ? target.GetComponent<GrayboxPlayerController>() : null;
        knockdown = target != null ? target.GetComponent<PlayerKnockdown>() : null;
        if (target != null) LockCursor();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if ((knockdown == null || !knockdown.IsDown) && !PlayerChat.BlocksInput && (NetworkLobby.Instance == null || NetworkLobby.Instance.InputAllowed) && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * MouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * MouseSensitivity, -85f, 85f);
        }

        if (target.GetComponent<NetworkPlayer>() == null)
            target.rotation = Quaternion.Euler(0f, yaw, 0f);
        float crouchAmount = playerController != null ? playerController.CrouchAmount : 0f;
        // Place the eyes in front of the torso so looking down shows its outer surface.
        transform.position = target.position + Vector3.up * Mathf.Lerp(eyeHeight, crouchEyeHeight, crouchAmount)
            + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * EyeForwardOffset;
        if (knockdown != null && knockdown.VisualAmount > 0)
            transform.position = knockdown.EyePosition + Quaternion.Euler(0, yaw, 0) * Vector3.forward * EyeForwardOffset;
        transform.rotation = Quaternion.Euler(pitch, yaw, knockdown != null ? 28 * knockdown.VisualAmount : 0);
    }

    public void SetSpectatorMode() { target = null; playerController = null; }

    public void SetTarget(Transform value)
    {
        target = value;
        playerController = target != null ? target.GetComponent<GrayboxPlayerController>() : null;
        knockdown = target != null ? target.GetComponent<PlayerKnockdown>() : null;
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
