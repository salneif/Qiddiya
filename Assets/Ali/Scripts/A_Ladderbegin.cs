using System;
using UnityEngine;

public class A_Ladderbegin : MonoBehaviour
{
    private PlayerController controller;

    private bool isOnLadder = false;

    public static event Action<A_Ladderbegin> OnBeginLadder;
    public static event Action<A_Ladderbegin> OnEndLadder;





    private void Start()
    {
        controller = PlayerController.instance;
    }
   
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.gameObject.CompareTag("Player"))
        {
            if (!isOnLadder)
            {
                controller.CanMove = false;
                Debug.Log("it worked");
                // we tell everyone that we made it to the ladder
                OnBeginLadder?.Invoke(this);
            }
            else
            {
                controller.CanMove = true;
                // we do the same here too
                OnEndLadder?.Invoke(this);
            }

        }
    }
}
