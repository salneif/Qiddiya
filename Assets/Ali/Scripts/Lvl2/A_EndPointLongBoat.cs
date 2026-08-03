using System;
using UnityEngine;

public class A_EndPointLongBoat : MonoBehaviour
{
    public event Action OnLongBoatRideEnded;
    private void OnTriggerEnter(Collider other)
    {
        OnLongBoatRideEnded?.Invoke();
    }
}
