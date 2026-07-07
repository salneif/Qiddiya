using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

public class LadderController : MonoBehaviour
{
   private static LadderController instance;

    public bool AlreadyOnLadderNow;

    private float input;
    private CharacterController characterController;

    [SerializeField] private float climbSpeed;


    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        A_Ladderbegin.OnBeginLadder += OnbeginLadder;
        A_Ladderbegin.OnEndLadder += OnEndLadder;

    }

   

    private void OnDisable()
    {
        A_Ladderbegin.OnBeginLadder -= OnbeginLadder;
        A_Ladderbegin.OnEndLadder -= OnEndLadder;
    }
    private void OnEndLadder(A_Ladderbegin ladderbegin)
    {
        AlreadyOnLadderNow = false;
    }

    private void OnbeginLadder(A_Ladderbegin obj)
    {
        AlreadyOnLadderNow = true;
    }

    private void Update()
    {
        if (AlreadyOnLadderNow)
        {
            if(input == 0)
            {
                return;
            }
            else
            {
                Vector3 climbDir = new Vector3(0, input ,0);
                characterController.Move(climbDir * Time.deltaTime * climbSpeed);
                Debug.Log("Worked");
            }
        }
    }

    public void OnInputLadder(InputAction.CallbackContext context)
    {
        input = context.ReadValue<float>();
    }

}
