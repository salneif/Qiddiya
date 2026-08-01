using System;
using UnityEngine;

public class A_boss : MonoBehaviour
{
    [SerializeField] private ParticleSystem shifingEffect;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource laugh;

    [Header("First Shift")]
    [SerializeField] private Transform firstPoint;


    private Transform _transFromPlaceLocation;
    public  event Action OnShiftingWorldToDark;


    private void OnEnable()
    {
        A_ShifingWorldToDarkFirstPoint.OnShiftingWorldToDarkBossEvent += OnShiftingWorldToDarkBossEvent;
    }
    private void OnDisable()
    {
        A_ShifingWorldToDarkFirstPoint.OnShiftingWorldToDarkBossEvent -= OnShiftingWorldToDarkBossEvent;
    }

    private void OnShiftingWorldToDarkBossEvent(int triggerNumber)
    {
        switch(triggerNumber)
        {
            case 0:
                _transFromPlaceLocation = firstPoint;
                laugh.Play();
                animator.SetTrigger("TriggerSwitch");
                shifingEffect.Play();
                break;
        }
    }


    private void OnTransformSpell()
    {
        transform.position = _transFromPlaceLocation.position;
        transform.rotation = _transFromPlaceLocation.rotation;
        OnShiftingWorldToDark?.Invoke();

    }
}
