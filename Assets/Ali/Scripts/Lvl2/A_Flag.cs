using System;
using Unity.Burst.Intrinsics;
using UnityEditor.PackageManager;
using UnityEngine;

public class A_Flag : MonoBehaviour
{
    [SerializeField] private Transform flag_Place;
    public event Action OnFlagPick;
    private void OnTriggerEnter(Collider other)
    {


        if (other.gameObject.CompareTag("Player"))
        {
            this.transform.SetParent(flag_Place);
            this.transform.localPosition = Vector3.zero + new Vector3(0.377f, -0.072f, -0.964f);
            this.transform.localRotation = Quaternion.identity;
            this.transform.localScale = Vector3.zero + new Vector3(7.34f,7.34f,7.34f);

            OnFlagPick?.Invoke();
        }
    }
}
