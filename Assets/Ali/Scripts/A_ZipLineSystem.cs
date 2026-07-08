using System.Net;
using UnityEngine;

public class A_ZipLineSystem : MonoBehaviour
{
    private PlayerController playerController;
    private A_CrouchAndJump playerCrouchAndJump;


    public bool WeAreInZipLine;


    [SerializeField] private Animator animator;

    [SerializeField] private GameObject currentGrip;
    [SerializeField] private float ziplineSpeed;
    private Vector3 currentEndPoint;
    

    
    private void Start()
    {
        playerController = PlayerController.instance;
        playerCrouchAndJump = A_CrouchAndJump.instance;
    }
    private void OnEnable()
    {
        A_ZipLineBegin.OnZipLineBegin += OnZipLineBegin;
    }
    // we dont want memory P here
    private void OnDisable()
    {
        A_ZipLineBegin.OnZipLineBegin -= OnZipLineBegin;
    }

    private void OnZipLineBegin(A_ZipLineBegin arg1, Vector3 ZiplineForward , GameObject grip , Vector3 endPoint )
    {
        if (!WeAreInZipLine)
        {
            animator.SetTrigger("OnZiplineBegin");
            transform.forward = ZiplineForward;
            WeAreInZipLine = true;
            playerController.CanMove = false;
            currentGrip = grip;
            currentEndPoint = endPoint;

            this.transform.SetParent(currentGrip.transform);
        }
    }

    private void Update()
    {
        if (!playerController.CanMove && WeAreInZipLine )
        {
            currentGrip.transform.position = Vector3.MoveTowards(
             currentGrip.transform.position,
             currentEndPoint,
             ziplineSpeed * Time.deltaTime
         );
        }
    }
}
