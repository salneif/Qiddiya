using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class A_Crouch : MonoBehaviour
{
    // soooooooo we need a way to talk to other scripts made by sultan and osama 
    // we will do that by event system 
    // here we will fire the event and tell movement code about it with the required varlibals
    public event Action<bool, float> OnCrouch;


    // we need to send some data with it 
    [Header("Crouch Numbers")]
    [SerializeField] private float crouchWalkSpeed;
    [SerializeField] private bool isCrouching = false;

    public void OnInputCrouch(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (isCrouching)
            {
                isCrouching = false;
            }
            else if (!isCrouching)
            {
                isCrouching = true;
            }
            OnCrouch?.Invoke(isCrouching, crouchWalkSpeed);
        }
       
    }
}
