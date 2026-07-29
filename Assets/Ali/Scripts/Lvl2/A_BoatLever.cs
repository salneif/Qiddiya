using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class A_BoatLever : MonoBehaviour
{
    [SerializeField] private GameObject rightSideWood;
    [SerializeField] private GameObject leftSideWood;

    [SerializeField] private int state = -1;


    public event Action<int> OnLeverSwitch;

    private bool _canInterAct = false;

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            _canInterAct = true;
        }
    }


    private void Start()
    {
        
    }
    private void Update()
    {
        
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if(context.started && _canInterAct)
        {
            if (state == -1)
            {
                state = 0;

                OnLeverSwitch?.Invoke(state);
            }
           else if(state == 0)
            {
                state = 1;

                OnLeverSwitch?.Invoke(state);
            }
            else if(state == 1)
            {
                state = 0;

                OnLeverSwitch?.Invoke(state);
            }
        }
    }
    
        
    
}

