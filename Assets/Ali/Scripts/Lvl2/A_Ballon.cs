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
            this.transform.localPosition = new Vector3(0.238f, -0.059f, -0.176f);
            this.transform.localScale = Vector3.one;

            OnBaloonPick?.Invoke();
        }
    }

    
}
