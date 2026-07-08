using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;

    [Header("Blend Targets (Speed parameter values, not world speeds)")]
    [Tooltip("Speed value fed to the blend trees while walking.")]
    [SerializeField] private float walkBlend = 0.5f;
    [Tooltip("Speed value fed to the Locomotion tree while running.")]
    [SerializeField] private float runBlend = 1f;
    [Tooltip("Speed value fed to the Crouch tree while crouch-walking.")]
    [SerializeField] private float crouchBlend = 0.5f;

    [Header("Blend Smoothing")]
    [Tooltip("Damp time for the Speed parameter. Higher = slower, smoother blend.")]
    [SerializeField] private float speedDampTime = 0.12f;

    [Header("Jump / Gravity")]
    [Tooltip("Desired peak jump height in metres.")]
    [SerializeField] private float jumpHeight = 1.4f;
    [Tooltip("Gravity magnitude (positive). 9.81 is real-world; games often use more.")]
    [SerializeField] private float gravity = 20f;
    [Tooltip("Small downward velocity kept while grounded so isGrounded stays reliable.")]
    [SerializeField] private float groundedStick = -2f;

    [Header("Input")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;

    // Cached parameter hashes — these are set every frame, so hashing once is the right reflex.
    private int speedHash;
    private int crouchHash;
    private int groundedHash;
    private int verticalHash;
    private int jumpHash;

    private bool isCrouching;
    private float verticalVelocity;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (controller == null) controller = GetComponent<CharacterController>();

        speedHash = Animator.StringToHash("Speed");
        crouchHash = Animator.StringToHash("IsCrouching");
        groundedHash = Animator.StringToHash("IsGrounded");
        verticalHash = Animator.StringToHash("VerticalSpeed");
        jumpHash = Animator.StringToHash("Jump");
    }

    private void Update()
    {
        HandleCrouch();
        HandleJumpAndGravity();
        UpdateLocomotion();
    }

    private void HandleCrouch()
    {
        if (Input.GetKeyDown(crouchKey))
            isCrouching = !isCrouching;

        animator.SetBool(crouchHash, isCrouching);
    }

    private void HandleJumpAndGravity()
    {
        bool grounded = controller.isGrounded;

        // Re-seat onto the ground each frame instead of letting gravity build up forever.
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = groundedStick;

        // Jump only from the ground, and not while crouched.
        if (grounded && !isCrouching && Input.GetKeyDown(jumpKey))
        {
            // v = sqrt(2 * g * h) gives exactly the velocity needed to reach jumpHeight.
            verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            animator.SetTrigger(jumpHash);
        }

        verticalVelocity -= gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);

        animator.SetBool(groundedHash, grounded);
        animator.SetFloat(verticalHash, verticalVelocity);
    }

    private void UpdateLocomotion()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        bool moving = Mathf.Abs(horizontal) > 0.01f ||
                      Mathf.Abs(vertical) > 0.01f;

        float targetSpeed = 0f;

        if (moving)
        {
            if (isCrouching)
                targetSpeed = crouchBlend;
            else if (Input.GetKey(runKey))
                targetSpeed = runBlend;
            else
                targetSpeed = walkBlend;
        }

        // Built-in damping: frame-rate independent and smoother than a manual Lerp.
        animator.SetFloat(speedHash, targetSpeed, speedDampTime, Time.deltaTime);
    }
}