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
    [SerializeField] private Transform Flag;

    [Header("Secound Shift")]
    [SerializeField] private Transform secoundPoint;
    [SerializeField] private ParticleSystem secoundSmokcBoom;
    [SerializeField] private Transform powerCrystol;


    [Header("third Shift")]
    [SerializeField] private Transform thirdpoint;
    [SerializeField] private AudioSource congratulationsSound;
    [SerializeField] private ParticleSystem JapanThing;



    [SerializeField] private bool runTest = false;

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
                Destroy(Flag.gameObject);
                break;
                case 1:
                _transFromPlaceLocation = secoundPoint;
                laugh.Play();
                animator.SetTrigger("Trigger2");
                shifingEffect.Play();
                transform.SetParent(null);
                Invoke("HandleHidePowerCrystol", 1);
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
    private void HandleHidePowerCrystol()
    {
        powerCrystol.gameObject.SetActive(false);
    }
    private void Start()
    {
       
    }

    public void OnLastShift()
    {
        _transFromPlaceLocation = thirdpoint;
        transform.position = _transFromPlaceLocation.position;
        transform.rotation = _transFromPlaceLocation.rotation;
        congratulationsSound.Play();
        shifingEffect.Play();
        JapanThing.Play();
        transform.SetParent(null);
    }
    private void Update()
    {
        if(isInBoatSection)
        {
            gameObject.transform.SetParent(boat.transform, true);
        }
        if (runTest)
        {
            OnShiftingWorldToDarkBossEvent(1);
            runTest = false;
        }
    }
}
