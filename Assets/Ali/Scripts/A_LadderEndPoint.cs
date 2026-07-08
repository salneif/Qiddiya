using System;
using UnityEngine;

public class A_LadderEndPoint : MonoBehaviour
{
    private PlayerController controller;

    private LadderController LadderController;

    public static event Action<A_LadderEndPoint, Vector3> OnEndLadder;


    private void Start()
    {
        controller = PlayerController.instance;
        LadderController = LadderController.instance;
    }


   
    
    private void OnTriggerEnter(Collider other)
    {
        // we do the same here too
        OnEndLadder?.Invoke(this, gameObject.transform.forward);

    }
}
