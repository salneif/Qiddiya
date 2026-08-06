using System;
using UnityEngine;

public class A_WaterDeathZone : MonoBehaviour
{
    public static event Action OnPlayerDrowning;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            OnPlayerDrowning?.Invoke();
        }
    }
}
