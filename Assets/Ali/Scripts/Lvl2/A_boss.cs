using System;
using UnityEngine;

public class A_boss : MonoBehaviour
{
    [SerializeField] private ParticleSystem shifingEffect;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource laugh;

    [Header("First Shift")]
    [SerializeField] private Transform firstPoint;
    [SerializeField] private ParticleSystem firstSmokcBoom;
    [SerializeField] private bool isInBoatSection;
    [SerializeField] private GameObject boat;

    [Header("Secound Shift")]
    [SerializeField] private Transform secoundPoint;
    


    private Transform _transFromPlaceLocation;
    public  event Action OnShiftingWorldToDark;


    private void OnEnable()
    {
        A_BossShifting.OnShiftingWorldToDarkBossEvent += OnShiftingWorldToDarkBossEvent;
    }
    private void OnDisable()
    {
        A_BossShifting.OnShiftingWorldToDarkBossEvent -= OnShiftingWorldToDarkBossEvent;
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
                case 1:
                _transFromPlaceLocation = secoundPoint;
                laugh.Play();
                animator.SetTrigger("TriggerSwitch");
                shifingEffect.Play();
                transform.SetParent(null);
                break;
        }
    }


    private void OnTransformSpell()
    {
        firstSmokcBoom.Play();
        transform.position = _transFromPlaceLocation.position;
        transform.rotation = _transFromPlaceLocation.rotation;
        OnShiftingWorldToDark?.Invoke();

        isInBoatSection = true;

    }
    private void Update()
    {
        if(isInBoatSection)
        {
            gameObject.transform.SetParent(boat.transform, true);
        }
    }
}
