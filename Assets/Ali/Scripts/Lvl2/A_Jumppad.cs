using System;
using UnityEngine;

public class A_Jumppad : MonoBehaviour
{
    public static event Action<float> OnJumppad;
    [SerializeField] private float jumppadForce;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            OnJumppad?.Invoke(jumppadForce);
        }
    }
}
