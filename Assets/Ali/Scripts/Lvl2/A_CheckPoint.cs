using System;
using UnityEngine;

public class A_CheckPoint : MonoBehaviour
{
    [SerializeField] private int checkPointNumber;
    private bool _isFirstTime = true;
    

    public static event Action<int> OnCheckPoint;
    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player") && _isFirstTime)
        {
            _isFirstTime = false;
            OnCheckPoint?.Invoke(checkPointNumber);
        }
    }
}
