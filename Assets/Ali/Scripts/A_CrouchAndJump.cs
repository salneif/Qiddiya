using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class A_CrouchAndJump : MonoBehaviour
{

    public static A_CrouchAndJump instance;
    // soooooooo we need a way to talk to other scripts made by sultan and osama 
    // we will do that by event system 
    // here we will fire the event and tell movement code about it with the required varlibals
    public event Action<bool, float> OnCrouch;
    public event Action<float> OnJump;


    // we need to send some data with it 
    [Header("Crouch Numbers")]
    [SerializeField] private float crouchWalkSpeed;
    [SerializeField] private bool isCrouching = false;

    [Header("Jump Numbers")]
    [SerializeField] private float jumpForce;

    private CharacterController characterController;


    private void Awake()
    {
        A_CrouchAndJump.instance = this;
    }
    private void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    public void OnInputCrouch(InputAction.CallbackContext context)
    {
        if (context.performed && characterController.isGrounded)
        {
            OnCrouch?.Invoke(isCrouching, crouchWalkSpeed);
            if (isCrouching)
            {
                isCrouching = false;
            }
            else if (!isCrouching)
            {
                isCrouching = true;
            }
           
        }
       
    }

    public void OnInputJump(InputAction.CallbackContext context)
    {
        

        if(context.performed && !isCrouching && characterController.isGrounded)
        {
            OnJump?.Invoke(jumpForce);
        }









     
    }
}
