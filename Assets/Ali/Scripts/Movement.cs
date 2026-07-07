using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{


    [Header("Movement")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float moveSpeed;

    [Header("Jump Settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float jumpForce = 7;
    [SerializeField] private float GroundDistanceCheck;
    [SerializeField] private bool OntheGround;
    [SerializeField] private Transform groundcheckPoint;
    [SerializeField] private bool canDoubleJump = false; // will be true ONLY WHEN WE JUMP FOR THE FIRST TIME!!!
    [SerializeField] private float airSpeedMultplier;
    private bool wasGorunded;

    [Header("Animation")]
    //[SerializeField] private Animator animator;
   

    private Vector2 moveInput;



    // Animation Var
    private bool isStanding;
    private bool isMoving;
    private bool isStartJumping;

    private void Update()
    {
       

        HandleMoving();
        HandleGroundCheck();
        SpeedControl();
        HandleAnimation();

        Debug.Log(rb.linearVelocity.magnitude);
    }


    private void HandleAnimation()
    {
        if(rb.linearVelocity.magnitude <= 1.5f)
        {
            isStanding = true;
            isMoving = false;
        }
        if(rb.linearVelocity.magnitude >= 1.51f)
        {
            isMoving = true;
            isStanding = false;
        }

      //  animator.SetBool("Standing", isStanding);
      //  animator.SetBool("Moving", isMoving);
      //  animator.SetBool("Jumping", isMoving);

    }

    private void HandleMoving()
    {
        Vector3 moveDirection = (transform.forward * moveInput.y) + (transform.right * moveInput.x);

        if(OntheGround)
        rb.AddForce(moveDirection.normalized * moveSpeed * 10 , ForceMode.Force);
        else
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10 * airSpeedMultplier, ForceMode.Force);

        }

    }


    private void HandleGroundCheck()
    {

        // we just draw the raycast so we can see it
        Debug.DrawRay(groundcheckPoint.transform.position, Vector3.down * GroundDistanceCheck, Color.red);
        // here we just send a line that starts from  groundcheckPoint with Vector.down as the way it will go with 
        // GroundDistanceCheck is the length of our line 
        // groundLayer is layer that we call ground in our world 
        OntheGround = (Physics.Raycast(groundcheckPoint.transform.position, Vector3.down, GroundDistanceCheck, groundLayer));

        
    }

    private void SpeedControl()
    {
        Vector3 flarVel = new Vector3(rb.linearVelocity.x,0f, rb.linearVelocity.z);

        if(flarVel.magnitude > moveSpeed)
        {
            Vector3 limtedVel = flarVel.normalized * moveSpeed;
            rb.linearVelocity = new Vector3(limtedVel.x,rb.linearVelocity.y,limtedVel.z);
        }
        if(moveInput == Vector2.zero)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        // here we just check if we clicked Space AND if we are on the ground (if any of them is false we dont jump ) and doubleJump
        if (context.started && (OntheGround || canDoubleJump))
        {
            // this line will help a lot when we do the double jump later
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

            // we just add the force with type Impulse (Impulse Is doing the full force of our jump in one frame )
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

           isStanding = false;
           isMoving = false;
           isStartJumping = true;


            if (canDoubleJump)
            {
                canDoubleJump = false;
            }
            else
            {
                canDoubleJump = true;
            }
        }
    }
}
