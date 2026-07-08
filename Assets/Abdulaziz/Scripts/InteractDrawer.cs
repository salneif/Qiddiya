using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class InteractDrawer : MonoBehaviour , IInteractable
{

    [SerializeField] private Animator animator;


    private bool _IsOpen;
    private bool _IsClosed;

    public void Interact()
    {

        animator.SetBool("Drawer Animation Open Test", false);
        animator.SetBool("Drawer Animation Close Test", false);
        animator.SetBool("Idle", true);


        if (_IsOpen == true && _IsClosed == false)
        {

            animator.SetBool("Drawer Animation Open Test", true);
            animator.SetBool("Drawer Animation Close Test", false);
            animator.SetBool("Idle", false);



            Debug.Log("the Drawer Animation Open Test is now awake.");
        }

        else if (_IsOpen == false && _IsClosed == true)
        {
            animator.SetBool("Drawer Animation Open Test", false);
            animator.SetBool("Drawer Animation Close Test", true);
            animator.SetBool("Idle", false);



            Debug.Log("the Drawer Animation Close Test is now awake.");
        } 

        else if (_IsOpen == false && _IsClosed == false)
        {
            animator.SetBool("Drawer Animation Open Test", false);
            animator.SetBool("Drawer Animation Close Test", false);
            animator.SetBool("Idle", true);

            Debug.Log("the Idle is now awake.");
        }


    }
}
