using System;
using UnityEngine;

public class A_ShifingWorldToDarkFirstPoint : MonoBehaviour
{

    [SerializeField] private int triggerNumber;

    public static event Action<int> OnShiftingWorldToDarkBossEvent;


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            OnShiftingWorldToDarkBossEvent?.Invoke(triggerNumber);
        }
    }
}
