using System;
using UnityEngine;

public class A_Ballon : MonoBehaviour
{
    [SerializeField] private Transform ballon_Place;
    public event Action OnBaloonPick;
    private void OnTriggerEnter(Collider other)
    {
        

        if (other.gameObject.CompareTag("Player"))
        {
            this.transform.parent = ballon_Place;
            this.transform.localPosition = Vector3.zero;

            OnBaloonPick?.Invoke();
        }
    }

    
}
