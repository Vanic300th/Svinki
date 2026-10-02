using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class GrayboxPlayerController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 10f;
    [SerializeField, Min(0f)] private float crouchSpeed = 2.5f;
    [SerializeField, Min(0.9f)] private float crouchHeight = 1.2f;
    [SerializeField, Min(0.1f)] private float stanceChangeSpeed = 6f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    private CharacterController characterController;
    private float verticalSpeed;
    private float standingHeight;
    private Vector3 standingCenter;
    private bool isCrouching;

    public float CrouchAmount => standingHeight > crouchHeight
        ? Mathf.Clamp01((standingHeight - characterController.height) / (standingHeight - crouchHeight))
        : 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        standingHeight = characterController.height;
        standingCenter = characterController.center;
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (Keyboard.current == null || cameraTransform == null)
            return;

        bool wantsCrouch = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        isCrouching = wantsCrouch || (isCrouching && !CanStandUp());
        float targetHeight = isCrouching ? Mathf.Min(crouchHeight, standingHeight) : standingHeight;
        characterController.height = Mathf.MoveTowards(characterController.height, targetHeight,
            stanceChangeSpeed * Time.deltaTime);
        characterController.center = standingCenter + Vector3.up * ((characterController.height - standingHeight) * 0.5f);

        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        Vector3 movement = forward * input.y + right * input.x;

        if (characterController.isGrounded)
        {
            verticalSpeed = -2f;
            if (!isCrouching && Keyboard.current.spaceKey.wasPressedThisFrame)
                verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        verticalSpeed += gravity * Time.deltaTime;
        bool sprinting = !isCrouching && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        float speed = isCrouching ? crouchSpeed : sprinting ? sprintSpeed : moveSpeed;
        characterController.Move((movement * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
    }

    public void SetCamera(Transform value) => cameraTransform = value;

    private bool CanStandUp()
    {
        float radius = Mathf.Max(0.01f, characterController.radius - characterController.skinWidth * 0.5f);
        Vector3 center = transform.TransformPoint(standingCenter);
        float halfSegment = Mathf.Max(0f, standingHeight * 0.5f - radius);
        Collider[] overlaps = Physics.OverlapCapsule(center - transform.up * halfSegment,
            center + transform.up * halfSegment, radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
            if (overlap != characterController && !overlap.transform.IsChildOf(transform))
                return false;
        return true;
    }
}
