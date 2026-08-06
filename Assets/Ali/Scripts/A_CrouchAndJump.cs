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
    public event Action<float, bool , bool> OnJump;


    // we need to send some data with it 
    [Header("Crouch Numbers")]
    [SerializeField] private float crouchWalkSpeed;
     public bool isCrouching = false;

    [Header("Jump Numbers")]
    [SerializeField] private float jumpForce;
    [SerializeField] private float jumpWindow = 0.5f;
    [SerializeField] private float _currentWidow;
    [SerializeField] private bool canJump;

    [Header("Ref")]
    [SerializeField] private A_Ballon a_Ballon;



    private CharacterController characterController;
    private bool canDoubleJump = false;


    private void Awake()
    {
        A_CrouchAndJump.instance = this;
    }
    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        if(a_Ballon != null )
        a_Ballon.OnBaloonPick += OnBaloonPick;
    }

    private void OnBaloonPick()
    {
        canDoubleJump = true;
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
    private void Update()
    {
        if (PlayerController.instance != null)
        {
            if (!PlayerController.instance.IsGrounded)
            {
                if (_currentWidow >= 0)
                {
                    _currentWidow -= Time.deltaTime;
                }
                else
                {
                    canJump = false;
                }
            }
            else
            {
                _currentWidow = jumpWindow;
                canJump = true;
            }
        }
    }
    public void OnInputJump(InputAction.CallbackContext context)
    {


        if (context.performed && !isCrouching && (canJump || canDoubleJump))
        {
            OnJump?.Invoke(jumpForce , canDoubleJump , canJump);
        }
    }
}
