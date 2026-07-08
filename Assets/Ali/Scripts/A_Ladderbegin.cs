using System;
using UnityEngine;

public class A_Ladderbegin : MonoBehaviour
{
    private PlayerController controller;

   private LadderController LadderController;

    public static event Action<A_Ladderbegin , Vector3> OnBeginLadder;
    public static event Action<A_Ladderbegin , Vector3> OnEndLadder;

    
    private void Start()
    {
        controller = PlayerController.instance;
        LadderController = LadderController.instance;
    }
   
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            
                Debug.Log("it worked");
                // we tell everyone that we made it to the ladder
                OnBeginLadder?.Invoke(this , gameObject.transform.forward);
            
           
              

        }
    }
    private void OnTriggerExit(Collider other)
    {
        // we do the same here too
        OnEndLadder?.Invoke(this, gameObject.transform.forward);

    }
}
