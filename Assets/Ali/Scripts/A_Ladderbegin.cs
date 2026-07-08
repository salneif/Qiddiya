using System;
using UnityEngine;

public class A_Ladderbegin : MonoBehaviour
{

   private LadderController LadderController;

    public static event Action<A_Ladderbegin , Vector3> OnBeginLadder;
    public static event Action<A_Ladderbegin , Vector3> OnEndLadder;

    
    private void Start()
    {
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
    
}
