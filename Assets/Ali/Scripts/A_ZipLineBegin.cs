using System;
using UnityEngine;

public class A_ZipLineBegin : MonoBehaviour
{
    [SerializeField] private GameObject grip;
    [SerializeField] private Transform endpointTransform;
    [SerializeField] private Vector3 endPoint;
    public static event Action<A_ZipLineBegin, Vector3 , GameObject , Vector3> OnZipLineBegin;




    private void Awake()
    {
        endPoint = endpointTransform.position;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {

            Debug.Log("it worked zipline");
            // we tell everyone that we made it to the ladder
            OnZipLineBegin?.Invoke(this, gameObject.transform.forward , grip , endPoint);




        }
    }
}
