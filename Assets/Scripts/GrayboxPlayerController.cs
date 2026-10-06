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
    private PlayerKnockdown knockdown;
    private readonly Collider[] standingOverlaps = new Collider[16];
    public bool Grounded => characterController != null && characterController.isGrounded;
    public float VerticalVelocity => verticalSpeed;
    public float MotionSpeed => characterController != null
        ? new Vector2(characterController.velocity.x, characterController.velocity.z).magnitude : 0;

    public float CrouchAmount => standingHeight > crouchHeight
        ? Mathf.Clamp01((standingHeight - characterController.height) / (standingHeight - crouchHeight))
        : 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        knockdown = GetComponent<PlayerKnockdown>();
        standingHeight = characterController.height;
        standingCenter = characterController.center;
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        // NetworkPlayer drives the owning client; remote transforms arrive through FishNet.
        if (GetComponent<NetworkPlayer>() != null) return;
        if (knockdown != null && knockdown.IsDown)
        { Simulate(Vector2.zero, transform.eulerAngles.y, false, false, false, Time.deltaTime); return; }
        if (PlayerChat.BlocksInput || NetworkLobby.Instance != null && !NetworkLobby.Instance.InputAllowed) return;
        if (Keyboard.current == null || cameraTransform == null)
            return;

        bool wantsCrouch = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        Vector2 input = ReadMoveInput();
        Simulate(input, cameraTransform.eulerAngles.y, Keyboard.current.spaceKey.wasPressedThisFrame,
            Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed,
            wantsCrouch, Time.deltaTime);
    }

    public static Vector2 ReadMoveInput()
    {
        if (Keyboard.current == null) return Vector2.zero;
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
        return Vector2.ClampMagnitude(input, 1f);
    }

    public void Simulate(Vector2 input, float yaw, bool jump, bool sprint, bool wantsCrouch, float deltaTime)
    {
        if (knockdown != null && knockdown.IsDown)
        {
            isCrouching = true;
            SetDownStance();
            if (characterController.isGrounded) verticalSpeed = -2;
            verticalSpeed += gravity * deltaTime;
            characterController.Move((knockdown.ConsumeSlide(deltaTime) + Vector3.up * verticalSpeed) * deltaTime);
            return;
        }
        input = Vector2.ClampMagnitude(input, 1f);
        isCrouching = wantsCrouch || (isCrouching && !CanStandUp());
        float targetHeight = isCrouching ? Mathf.Min(crouchHeight, standingHeight) : standingHeight;
        characterController.height = Mathf.MoveTowards(characterController.height, targetHeight,
            stanceChangeSpeed * deltaTime);
        characterController.center = standingCenter + Vector3.up * ((characterController.height - standingHeight) * 0.5f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 movement = forward * input.y + right * input.x;

        if (characterController.isGrounded)
        {
            verticalSpeed = -2f;
            if (!isCrouching && jump)
                verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        verticalSpeed += gravity * deltaTime;
        bool sprinting = !isCrouching && sprint;
        float speed = isCrouching ? crouchSpeed : sprinting ? sprintSpeed : moveSpeed;
        characterController.Move((movement * speed + Vector3.up * verticalSpeed) * deltaTime);
    }

    public void SetRemoteStance(float amount)
    {
        if (knockdown != null && knockdown.IsDown) { SetDownStance(); return; }
        characterController.height = Mathf.Lerp(standingHeight, Mathf.Min(crouchHeight, standingHeight), Mathf.Clamp01(amount));
        characterController.center = standingCenter + Vector3.up * ((characterController.height - standingHeight) * .5f);
    }

    public void SetCamera(Transform value) => cameraTransform = value;

    private void SetDownStance()
    {
        characterController.height = Mathf.Max(characterController.radius * 2, .65f);
        characterController.center = standingCenter + Vector3.up * ((characterController.height - standingHeight) * .5f);
    }

    private bool CanStandUp()
    {
        float radius = Mathf.Max(0.01f, characterController.radius - characterController.skinWidth * 0.5f);
        Vector3 center = transform.TransformPoint(standingCenter);
        float halfSegment = Mathf.Max(0f, standingHeight * 0.5f - radius);
        int count = gameObject.scene.GetPhysicsScene().OverlapCapsule(center - transform.up * halfSegment,
            center + transform.up * halfSegment, radius, standingOverlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider overlap = standingOverlaps[i];
            if (overlap != characterController && !overlap.transform.IsChildOf(transform))
                return false;
        }
        return true;
    }
}
