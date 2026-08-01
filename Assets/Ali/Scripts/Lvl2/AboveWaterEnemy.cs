using UnityEngine;

public class AboveWaterEnemy : MonoBehaviour
{
    [SerializeField] private Transform FaceingForward;
    [SerializeField] private Animator animator;
    [SerializeField] private float turnSpeed;


    private bool isfaceingForward = true;
    // Vectors
    private Vector3 forwardVector;
    private Vector3 backwardVector;


    //Ref
    [SerializeField] private LongBoatSectionManager longBoatSectionManager;

    private Quaternion targetRotation;
    private bool isTurning;
    

    private void Awake()
    {
        forwardVector = FaceingForward.forward;
        backwardVector = -FaceingForward.forward;
    }
    private void OnEnable()
    {
        longBoatSectionManager.OnturnAround += OnturnAround;
    }
    private void OnDisable()
    {
        longBoatSectionManager.OnturnAround -= OnturnAround;
    }

    private void OnturnAround(int turnNum)
    {
        animator.SetTrigger("Turn");
        Vector3 dir = Vector3.zero;

        if (gameObject.CompareTag("RightEnemy"))
        {
            if (turnNum == 0)
            {
                dir = forwardVector;
            }
            else if (turnNum == 1)
            {
                dir = backwardVector;
            }
           
        }
        else if (gameObject.CompareTag("LeftEnemy"))
        {
            if (turnNum == 0)
            {
                dir = backwardVector;
            }
            else if (turnNum == 1)
            {
                dir = forwardVector;
            }
        }
        targetRotation = Quaternion.LookRotation(dir);
        isTurning = true;
    }

    private void Update()
    {
        if (isTurning)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,turnSpeed * Time.deltaTime);

            if(Quaternion.Angle(transform.rotation,targetRotation) < 0.1)
            {
                isTurning=false;
            }
        }
    }


}
