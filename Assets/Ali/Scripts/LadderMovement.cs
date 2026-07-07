using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class LadderMovement : MonoBehaviour
{
    private static LadderMovement instance;

    public bool CanMoveInLadder;



    private void Awake()
    {
        instance = this;
    }


    private void OnLadderMove(InputAction.CallbackContext context)
    {

    }


}
