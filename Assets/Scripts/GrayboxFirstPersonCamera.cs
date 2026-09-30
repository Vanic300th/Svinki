using UnityEngine;
using UnityEngine.InputSystem;

public class GrayboxFirstPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float eyeHeight = 1.65f;
    [SerializeField] private float mouseSensitivity = 0.12f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        if (target != null)
            yaw = target.eulerAngles.y;
        LockCursor();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null &&
                 Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }

        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -200f, 200f);
        }

        target.rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.position = target.position + Vector3.up * eyeHeight;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public void SetTarget(Transform value) => target = value;

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
