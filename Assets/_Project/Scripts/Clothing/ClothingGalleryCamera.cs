using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Free camera for inspecting the development clothing gallery.</summary>
[RequireComponent(typeof(Camera))]
public sealed class ClothingGalleryCamera : MonoBehaviour
{
    private void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;
        if (!mouse.rightButton.isPressed || keyboard.escapeKey.isPressed)
        {
            ReleaseCursor();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Vector2 delta = mouse.delta.ReadValue() * .12f;
        Vector3 angles = transform.eulerAngles;
        float pitch = Mathf.Clamp(Mathf.DeltaAngle(0, angles.x) - delta.y, -85, 85);
        transform.rotation = Quaternion.Euler(pitch, angles.y + delta.x, 0);

        Vector3 direction = Vector3.zero;
        if (keyboard.wKey.isPressed) direction += transform.forward;
        if (keyboard.sKey.isPressed) direction -= transform.forward;
        if (keyboard.dKey.isPressed) direction += transform.right;
        if (keyboard.aKey.isPressed) direction -= transform.right;
        if (keyboard.eKey.isPressed) direction += Vector3.up;
        if (keyboard.qKey.isPressed) direction -= Vector3.up;
        float speed = keyboard.leftShiftKey.isPressed ? 16 : 6;
        transform.position += Vector3.ClampMagnitude(direction, 1) * speed * Time.unscaledDeltaTime;
    }

    private void OnDisable() => ReleaseCursor();

    private static void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
