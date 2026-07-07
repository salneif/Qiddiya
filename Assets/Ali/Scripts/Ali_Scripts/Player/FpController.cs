using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class FpController : MonoBehaviour
{
    public static FpController instance {  get; private set; }


   public bool CanMove {  get; private set; } = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float CrouchSpeed = 1.5f;
    [SerializeField] private float slopeSpeed = 10f;


    [Header("Look")]
    [SerializeField, Range(0.1f, 10f)] private float lookSpeedX = 2f;
    [SerializeField, Range(0.1f, 10f)] private float lookSpeedY = 2f;
    [SerializeField, Range(1f, 180f)] private float LookUpLimit = 80f;
    [SerializeField, Range(1f, 180f)] private float LookDownLimit = 80f;

    [Header("jump")]
    [SerializeField] private float jumpForce;
    [SerializeField] private float gravity = 30f;


    [Header("Crouch")]
    [SerializeField] private float crouchHeight = 0.5f;
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float timeToCrouch = 0.25f;
    [SerializeField] private Vector3 crouchingCenter = new Vector3(0,1f,0);
    [SerializeField] private Vector3 standingCenter = new Vector3(0, 0, 0);
    private bool isCrouching;
    private bool duringCrouchAnimation;


    [Header("HeadBob")]
    [SerializeField] private float walkBobSpeed = 14f;
    [SerializeField] private float walkBobAmount = 0.05f;
    [SerializeField] private float sprintBobSpeed = 18f;
    [SerializeField] private float sprintBobAmount = 0.1f;
    [SerializeField] private float crouchBobspeed = 8f;
    [SerializeField] private float crouchBobAmount = 0.025f;
    private float defaultYPos = 0;
    private float timer;



    [Header("Footstep")]
    [SerializeField] private float baseStepSpeed = 0.5f;
    [SerializeField] private float crouchStepMultipler = 1.5f;
    [SerializeField] private float sprintStepMultipler = 1.5f;
    [SerializeField] private AudioSource footStepAudioSource = default;
    [SerializeField] private AudioClip[] woodClips = default;
    [SerializeField] private AudioClip[] metalClips = default;
    [SerializeField] private AudioClip[] grassClips = default;
    private float footsStepTimer = 0;
    private float GetCurrentOffset => isCrouching ? baseStepSpeed * crouchStepMultipler : isSprinting ? baseStepSpeed * sprintStepMultipler : baseStepSpeed;


    [Header("Interact")]
    [SerializeField] private Camera mainCamera;
    private bool canInteract;

    //Sliding Parmeters

    private Vector3 hitPointNormal;
    private bool IsSliding
    {
        get
        {
            if(characterController.isGrounded && Physics.Raycast(transform.position,Vector3.down , out RaycastHit slopeHit , 2f))
            {
                hitPointNormal = slopeHit.normal;
                return Vector3.Angle(hitPointNormal, Vector3.up) > characterController.slopeLimit;
            }
            else
            {
                return false;
            }
        }
    }

    private Camera playerCamera;
    private CharacterController characterController;

    private Vector3 moveDirection;
    private Vector2 currentInput;
    private Vector2 moveInput;
    private Vector2 lookInput;


    private bool isSprinting = false;

    private float rotationX = 0;

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }


        characterController = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        defaultYPos = playerCamera.transform.localPosition.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (CanMove)
        {
            HandleMoveSpeed();
            HandleMovewmentInput();
            HandleMouseMovement();
            HandleHeadBob();
            HandleFootStepsSound();
            ApplyFinalMovement();

            HandleInteract();
        }
    }
    private void HandleMovewmentInput()
    {
        currentInput = new Vector2(moveSpeed * moveInput.x, moveSpeed * moveInput.y);

        float moveDirctionY = moveDirection.y;
        moveDirection = (transform.TransformDirection(Vector3.forward) * currentInput.y) + (transform.TransformDirection(Vector3.right) * currentInput.x);
        moveDirection.y = moveDirctionY;
    }

    private void HandleMouseMovement()
    {
        rotationX -= lookInput.y * lookSpeedY;
        rotationX = Mathf.Clamp(rotationX,-LookUpLimit, LookDownLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0,lookInput.x * lookSpeedX , 0);
    }

    private void ApplyFinalMovement()
    {
        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }
        if(IsSliding)
        {
            moveDirection += new Vector3 (hitPointNormal.x,-hitPointNormal.y , hitPointNormal.z) * slopeSpeed    ;
        }

        characterController.Move(moveDirection * Time.deltaTime);

    }
   private void HandleMoveSpeed()
    {
        if (isSprinting)
        {
            moveSpeed = sprintSpeed;
        }
        else if(!isSprinting)
        {
            if (isCrouching)
            {
                moveSpeed = CrouchSpeed;
            }
            else
            {
                moveSpeed = walkSpeed;
            }
        }
    }

    private void HandleJump()
    {
        moveDirection.y = jumpForce;
    }
    private void HandleCrouch()
    {
        isSprinting = false;
        StartCoroutine(CrouchStand());
    }

    private IEnumerator CrouchStand()
    {
        // we just check if we are under something 
        if(isCrouching && Physics.Raycast(playerCamera.transform.position , Vector3.up,1f))
            yield break;

        duringCrouchAnimation = true;

        float timeElapsed = 0;
        float targetHeigght = isCrouching ? standingHeight : crouchHeight;
        float currentHeight = characterController.height;
        Vector3 targetCenter = isCrouching ? standingCenter : crouchingCenter;
        Vector3 currentCenter = characterController.center;

        while(timeElapsed < timeToCrouch)
        {
            characterController.height = Mathf.Lerp(currentHeight, targetHeigght, timeElapsed/timeToCrouch);
            characterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / timeToCrouch);
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        // here we just make sure we are in the right place
        characterController.height = targetHeigght;
        characterController.center = targetCenter;

        isCrouching = !isCrouching;

        duringCrouchAnimation = false;
    }

    private void HandleHeadBob()
    {
        if(!characterController.isGrounded) return;
        // we just check if we have movement no matter what dir we are going
        if(Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z) > 0.1f)
        {
            timer += Time.deltaTime * (isCrouching ? crouchBobspeed : isSprinting ? sprintBobSpeed : walkBobSpeed);
            playerCamera.transform.localPosition = new Vector3(
                playerCamera.transform.localPosition.x,
                defaultYPos + Mathf.Sin(timer) * (isCrouching ? crouchBobAmount : isSprinting ? sprintBobAmount : walkBobAmount),
                playerCamera.transform.localPosition.z);
        }
    }

    private void HandleFootStepsSound()
    {
        if(!characterController.isGrounded || moveInput == Vector2.zero)return;

        footsStepTimer -= Time.deltaTime;

        if(footsStepTimer <= 0)
        {
            footStepAudioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            if(Physics.Raycast(playerCamera.transform.position , Vector3.down, out RaycastHit hit , 3))
            {
                
                switch (hit.collider.tag)
                {
                    case "Wood":
                        footStepAudioSource.PlayOneShot(woodClips[UnityEngine.Random.Range(0, woodClips.Length )]);
                        break;
                    case "Metal":
                        footStepAudioSource.PlayOneShot(metalClips[UnityEngine.Random.Range(0, metalClips.Length )]);
                        break ;
                    case "Grass":
                        footStepAudioSource.PlayOneShot(grassClips[UnityEngine.Random.Range(0, grassClips.Length )]);
                        break;
                    default:
                        break;
                }
            }
            footsStepTimer = GetCurrentOffset;
        }
    }

    private void HandleInteract()
    {
       if( Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out RaycastHit hit , 7))
        {
            if (hit.collider.TryGetComponent<IInteractable>(out IInteractable target))
            {
               if(canInteract)
                target.Interact();
            }
            else
            {
                Debug.Log("didnt work lil bro");
            }
        }
      
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }
    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.started && characterController.isGrounded && !isCrouching)
        {
            isSprinting = true;
        }
        else if (context.canceled)
        {
            isSprinting = false;
        }
    }
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && characterController.isGrounded)
        {
            HandleJump();
        }
    }
    public void OnCrouch(InputAction.CallbackContext context)
    {
        if(context.performed && !duringCrouchAnimation &&  characterController.isGrounded)
        {
            HandleCrouch();
        }
    }
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            canInteract = true;
        }
        else if (context.canceled)
        {
            canInteract = false;
        }
    }
}
