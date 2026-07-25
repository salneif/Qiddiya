using System;
using System.Collections;
using UnityEngine;

public class A_Jumppad : MonoBehaviour
{
    public static event Action<float> OnJumppad;
    [SerializeField] private float jumppadForce;
    [SerializeField] private Vector3 targetScale;
    [SerializeField] private float scaleTime;

   [SerializeField] private AudioSource jumpPadSound;
    private Vector3 orignalScale;

    private void Awake()
    {
        orignalScale = transform.localScale;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            OnJumppad?.Invoke(jumppadForce);
            StartCoroutine(HandleScaling());
            jumpPadSound.Play();
        }
    }
    private IEnumerator HandleScaling()
    {
        float passedTime = 0f;

        while(passedTime < scaleTime)
        {
            passedTime += Time.deltaTime;
            float time = passedTime / scaleTime;
            transform.localScale = Vector3.Lerp(orignalScale, targetScale, time);
            yield return null;
        }

        passedTime = 0f;
        while (passedTime < scaleTime)
        {
            passedTime += Time.deltaTime;
            float time = passedTime / scaleTime;
            transform.localScale = Vector3.Lerp(targetScale, orignalScale, time);
            yield return null;
        }
        transform.localScale = orignalScale;
    }
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.gameObject.CompareTag("Player"))
        {
            OnJumppad?.Invoke(jumppadForce);
            StartCoroutine(HandleScaling());
            jumpPadSound.Play();
        }
    }
}
