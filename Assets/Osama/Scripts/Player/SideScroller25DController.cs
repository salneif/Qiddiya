using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SideScroller25DController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";

    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 5.5f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Crouch")]
    [SerializeField] private float crouchHeightScale = 0.55f;
    [SerializeField] private float crouchLerpSpeed = 10f;

    [Header("2.5D Lane Constraint")]
    [Tooltip("المحور الذي تتحرك عليه الشخصية يمين ويسار (المحور الجانبي الأساسي للحركة)")]
    [SerializeField] private Vector3 lateralAxis = Vector3.right;
    [Tooltip("محور العمق (أمام/خلف) المسموح بحركة محدودة عليه")]
    [SerializeField] private Vector3 depthAxis = Vector3.forward;
    [Tooltip("أقصى مسافة يمكن للشخصية الابتعاد بها عن خط البداية على محور العمق")]
    [SerializeField] private float depthRange = 0.6f;
    [SerializeField] private float depthSpeedScale = 0.5f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.3f;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    private Vector2 moveInput;
    private bool sprintHeld;
    private bool crouchHeld;
    private bool jumpQueued;

    private float verticalVelocity;
    private float depthOrigin;
    private float standingHeight;

    private Vector3 LateralDir => lateralAxis.normalized;
    private Vector3 DepthDir => depthAxis.normalized;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        standingHeight = controller.height;
        depthOrigin = Vector3.Dot(transform.position, DepthDir);

        var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
        moveAction = map.FindAction("Move", throwIfNotFound: true);
        jumpAction = map.FindAction("Jump", throwIfNotFound: true);
        sprintAction = map.FindAction("Sprint", throwIfNotFound: true);
        crouchAction = map.FindAction("Crouch", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
        crouchAction.Enable();
        jumpAction.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJumpPerformed;
        moveAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
        crouchAction.Disable();
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx) => jumpQueued = true;

    private void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        sprintHeld = sprintAction.IsPressed();
        crouchHeld = crouchAction.IsPressed();

        UpdateCrouchHeight();
        UpdateMovement();
    }

    private void UpdateCrouchHeight()
    {
        float targetHeight = crouchHeld ? standingHeight * crouchHeightScale : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchLerpSpeed);
        controller.center = new Vector3(controller.center.x, controller.height / 2f, controller.center.z);
    }

    private void UpdateMovement()
    {
        bool grounded = IsGrounded();
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        float speed = crouchHeld ? crouchSpeed : (sprintHeld ? sprintSpeed : walkSpeed);

        Vector3 lateralMotion = LateralDir * moveInput.x * speed;
        Vector3 depthMotion = DepthDir * ClampedDepthInput() * speed * depthSpeedScale;

        if (jumpQueued && grounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        jumpQueued = false;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = lateralMotion + depthMotion + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);

        FaceLateralDirection(lateralMotion);
    }

    private float ClampedDepthInput()
    {
        float depthInput = moveInput.y;
        float currentDepth = Vector3.Dot(transform.position, DepthDir) - depthOrigin;

        if (currentDepth >= depthRange && depthInput > 0f) return 0f;
        if (currentDepth <= -depthRange && depthInput < 0f) return 0f;
        return depthInput;
    }

    private void FaceLateralDirection(Vector3 lateralMotion)
    {
        if (lateralMotion.sqrMagnitude < 0.0001f) return;
        Quaternion targetRotation = Quaternion.LookRotation(lateralMotion.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.down, out _, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
    }
}
