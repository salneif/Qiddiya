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
            this.transform.SetParent(ballon_Place);
            this.transform.localPosition = Vector3.zero+ new Vector3 (0.004f, 1.075f, -0.29f);
            this.transform.localRotation = Quaternion.identity;
            this.transform.localScale = Vector3.one;

            OnBaloonPick?.Invoke();
        }
    }

    
}
