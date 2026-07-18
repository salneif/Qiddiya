using System;
using UnityEngine;

public class A_WaterSection : MonoBehaviour
{
    // Refrencees
    private A_CrouchAndJump a_CrouchAndJump;
   [SerializeField] private A_WaterEnemySystem a_WaterEnemySystem;

    public static event Action OnEnemySeeingPlayer;

    private void Start()
    {
        a_CrouchAndJump = A_CrouchAndJump.instance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") && !a_WaterEnemySystem.isUnderWater)
        {
            OnEnemySeeingPlayer?.Invoke();
        }
    }
}
