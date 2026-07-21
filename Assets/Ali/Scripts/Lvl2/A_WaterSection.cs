using System;
using UnityEngine;

public class A_WaterSection : MonoBehaviour
{
    // Refrencees
    private A_CrouchAndJump a_CrouchAndJump;
   [SerializeField] private A_WaterEnemySystem a_WaterEnemySystem;
   

    public static event Action OnEnemySeeingPlayer;
    private bool isFirstTime = true;

    private void Start()
    {
        a_CrouchAndJump = A_CrouchAndJump.instance;
    }

    private void OnTriggerStay(Collider other)
    {
        // we check if we are underwater
        // and if it is first time so we dont retrigger it again 
        // and if the player is not Crouching
        if (other.gameObject.CompareTag("Player") && !a_WaterEnemySystem.isUnderWater && isFirstTime &&!a_CrouchAndJump.isCrouching )
        {
            isFirstTime = false;
            OnEnemySeeingPlayer?.Invoke();
        }
    }
}
