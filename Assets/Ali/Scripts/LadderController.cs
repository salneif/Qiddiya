using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

public class LadderController : MonoBehaviour
{
   public static LadderController instance;
   public bool AlreadyOnLadderNow = false ;
    private CharacterController characterController;
    private PlayerController playerController;


    private float input;
    private bool isGetingOutOftheLadder;
    private Vector3 thisladderForward;
    [SerializeField] private float timeToStopMovingTheplayer;
    [SerializeField] private float CurrentTimeTostopMovingTheplayer;
    [SerializeField] private float speedToGtOutOftheLadder;
    

    [SerializeField] private float climbSpeed;

    [Header("Animation")]
    [SerializeField] private Animator animator;




    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        playerController = PlayerController.instance;
    }

    private void OnEnable()
    {
        A_Ladderbegin.OnBeginLadder += OnbeginLadder;
        A_LadderEndPoint.OnEndLadder += OnEndLadder;

    }

   

    private void OnDisable()
    {
        A_Ladderbegin.OnBeginLadder -= OnbeginLadder;
        A_LadderEndPoint.OnEndLadder -= OnEndLadder;
    }
    private void OnEndLadder(A_LadderEndPoint ladderEnd , Vector3 ladderForward)
    {
       if(AlreadyOnLadderNow)
        {
            animator.ResetTrigger("StartLadder");
            animator.SetTrigger("EndLadder");
            isGetingOutOftheLadder = true ;
            CurrentTimeTostopMovingTheplayer = timeToStopMovingTheplayer ;
            thisladderForward = -ladderForward;
            AlreadyOnLadderNow =false;
            animator.SetBool("isOnLadder", false);
        }
        
    }

    private void OnbeginLadder(A_Ladderbegin obj , Vector3 ladderForward)
    {
        if (!AlreadyOnLadderNow)
        {
            animator.ResetTrigger("EndLadder");
            animator.SetTrigger("StartLadder");
            AlreadyOnLadderNow = true;
            animator.SetBool("isOnLadder", true);
            playerController.CanMove = false;

            // we need player to face the ladder 
            transform.forward = -ladderForward;
            Debug.Log("it worked yooooooo");
        }



    }

    private void Update()
    {
       
        
        if (isGetingOutOftheLadder && CurrentTimeTostopMovingTheplayer > 0)
        {
            CurrentTimeTostopMovingTheplayer -= Time.deltaTime;
            Vector3 movedir =  thisladderForward;
            characterController.Move(movedir * speedToGtOutOftheLadder * Time.deltaTime);
            if(CurrentTimeTostopMovingTheplayer < 0)
            {
                isGetingOutOftheLadder = false;
                playerController.CanMove = true;
            }
        }
        if (AlreadyOnLadderNow)
        {
            if (input == 0)
            {
                animator.SetFloat("MovementDirInLadder", 0);
                return;
            }
            else
            {
                animator.SetFloat("MovementDirInLadder", input);
                Vector3 climbDir = new Vector3(0, input, 0);
                characterController.Move(climbDir * Time.deltaTime * climbSpeed);

            }
          }
         }

    public void OnInputLadder(InputAction.CallbackContext context)
    {
        input = context.ReadValue<float>();
    }

}
