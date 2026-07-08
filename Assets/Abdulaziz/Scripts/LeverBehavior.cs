using System;
using System.Collections;
using UnityEngine;

public class LeverBehavior : MonoBehaviour, IInteractable
{
    public event Action Pulled;
    // private as it cannot be viewed by other scripts (classes), but it can be seen by the inspector.
    [SerializeField] private Transform handle;
    [SerializeField] private float pulledAngle = 45f;
    [SerializeField] private float pullSpeed = 180f;
    
    // this is private and CANNOT be seen by the inspector.
    private bool isPulled;

    // calls the IInteractable interface.
    public void Interact()
    {
        // Here it checks the condition if it's pulled or not. If it is, it will return without anything. If it isn't,
        // then it will run the code.
        if (isPulled) return;
        isPulled = true;

        StopAllCoroutines();
        StartCoroutine(PullRoutine());
        Pulled?.Invoke();
        Debug.Log("Lever pulled.");
    }

    private IEnumerator PullRoutine()
    {
        Quaternion start = handle.localRotation;
        Quaternion end = start * Quaternion.Euler(pulledAngle, 0f, 0f);

        while (Quaternion.Angle(handle.localRotation, end) > 0.1f)
        {
            handle.localRotation = Quaternion.RotateTowards(
                handle.localRotation, end, pullSpeed * Time.deltaTime);
            yield return null;
        }
        handle.localRotation = end;
    }
}