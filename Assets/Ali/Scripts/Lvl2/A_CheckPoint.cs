using System;
using UnityEngine;

public class A_CheckPoint : MonoBehaviour
{
    [SerializeField] private int checkPointNumber;
    private bool _isFirstTime = true;

    [SerializeField] private ParticleSystem particleBeforeCheckPoint;
    [SerializeField] private ParticleSystem particleAfterCheckPoint;
    [SerializeField] private ParticleSystem particleLooping;
    

    public static event Action<int> OnCheckPoint;
    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player") && _isFirstTime)
        {
            _isFirstTime = false;
            OnCheckPoint?.Invoke(checkPointNumber);
            particleBeforeCheckPoint.Stop();
            particleAfterCheckPoint.Play();
            Invoke("HandleLoopingPartical", 1);
        }
    }

    private void HandleLoopingPartical()
    {
        particleLooping.Play();
    }
}
