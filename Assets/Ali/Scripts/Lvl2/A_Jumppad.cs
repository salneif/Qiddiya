using System;
using UnityEngine;

public class A_Jumppad : MonoBehaviour
{
    public static event Action OnJumppad;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            OnJumppad?.Invoke();
        }
    }
}
