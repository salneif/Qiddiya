using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;


public class LongBoatSectionManager : MonoBehaviour
{
    [SerializeField] private float timeToTurnAround;
    [SerializeField] private float _currentTime;
    [SerializeField] private int CurrentTurnNum = 0;
    [SerializeField] private GameObject lightRight;
    [SerializeField] private GameObject lightLeft;
    [SerializeField] private AudioSource rightSound;
    [SerializeField] private AudioSource leftSound;

    private int  rnadomInt;
    private int lastRandomInt =-1;

    [SerializeField] private float speedUpAmount;
    [SerializeField] private float originalSpeedUpTime;
    [SerializeField] private float fastestTimeToTurn = 1000;



    public event Action<int> OnturnAround;
    public event Action OnDeathInLongBoat;

    private int _currentWoodBlockNum;
   [SerializeField] private bool _hasDiedBefore = false;
   [SerializeField] private bool _inCahngeState = false;

   [SerializeField] private bool _inDangerZone = false;
    //ref
    [SerializeField] private A_BoatLever a_BoatLever;
    [SerializeField] private A_PlayerDeath_WaterSection a_PlayerDeath;



    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            _inDangerZone = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            _inDangerZone = false;
        }
    }
    private void Awake()
    {
        _currentTime = 1.6f;
        speedUpAmount = originalSpeedUpTime;

        lightLeft.SetActive(false);
        lightRight.SetActive(true);

    }
    private void OnEnable()
    {
        a_BoatLever.OnLeverSwitch += OnLeverSwitch;
        a_PlayerDeath.OnPlayerDeath += OnPlayerDeath;
    }

   

    private void OnDisable()
    {
        a_BoatLever.OnLeverSwitch -= OnLeverSwitch;
        a_PlayerDeath.OnPlayerDeath -= OnPlayerDeath;
    }
    private void OnPlayerDeath()
    {
        _inDangerZone = false ;
        speedUpAmount = originalSpeedUpTime ;
        _currentTime = timeToTurnAround;
        fastestTimeToTurn = 100;
        
    }

    private void OnLeverSwitch(int currentWoodBlockNum)
    {
        _currentWoodBlockNum = currentWoodBlockNum;
    }

    private void Update()
    {
        if (_inDangerZone)
        {
            if (_currentTime > 0)
            {
                _currentTime -= Time.deltaTime;
                if (_currentTime <= 0)
                {
                    
                    rnadomInt = UnityEngine.Random.Range(0, 2);
                    // this means we are turing left 
                    if (CurrentTurnNum == 0)
                    {
                        if (rnadomInt == CurrentTurnNum && lastRandomInt != rnadomInt)
                        {
                            lastRandomInt = rnadomInt;
                            _inCahngeState = true;

                            lightLeft.SetActive(false);
                            lightRight.SetActive(true);

                            rightSound.Play();
                            Invoke("HandleActiveDeathFromZero", 1f);
                        }
                        else
                        {
                            lastRandomInt = -1;
                            // this means we are turing left 
                            CurrentTurnNum = 1;
                            OnturnAround?.Invoke(1);
                            _inCahngeState = true;

                            lightLeft.SetActive(true);
                            lightRight.SetActive(false);

                            leftSound.Play();
                            Invoke("HandleActiveDeathFromZero", 1f);
                        }
                    }

                    else if (CurrentTurnNum == 1)
                    {
                        if (rnadomInt == CurrentTurnNum && lastRandomInt != rnadomInt)
                        {
                            lastRandomInt = rnadomInt;
                            _inCahngeState = true;

                            lightLeft.SetActive(true);
                            lightRight.SetActive(false);

                            leftSound.Play();
                            Invoke("HandleActiveDeathFromOne", 1f);
                        }
                        // this means we are turing right 
                        else
                        {
                            lastRandomInt = -1;
                            CurrentTurnNum = 0;
                            OnturnAround?.Invoke(0);
                            _inCahngeState = true;

                            lightLeft.SetActive(false);
                            lightRight.SetActive(true);

                            rightSound.Play();
                            Invoke("HandleActiveDeathFromOne", 1f);
                        }


                    }
                    if (fastestTimeToTurn > 1.6f)
                    {
                        speedUpAmount += speedUpAmount;
                        fastestTimeToTurn = timeToTurnAround - speedUpAmount;
                        
                    }
                  _currentTime = fastestTimeToTurn;
                }
            }
            if (_inDangerZone)
            {

                switch (_currentWoodBlockNum)
                {

                    case -1:
                        if(!_inCahngeState)

                        // invoke death
                        ActiveDeath();
                        break;

                    case 0:
                        if (CurrentTurnNum != 0 && !_inCahngeState)
                        {
                            ActiveDeath();
                            Debug.Log("die000000");

                        }
                        break;

                    case 1:
                        if (CurrentTurnNum != 1 && !_inCahngeState)
                        {
                            ActiveDeath();
                            Debug.Log("die1");

                        }
                        break;
                }
            }
        }
        
    }

    private void HandleActiveDeathFromZero()
    {
        _inCahngeState = false;
    }
    private void HandleActiveDeathFromOne()
    {
        _inCahngeState = false;
    }

    private void ActiveDeath()
    {
        if(_hasDiedBefore)return;


        _hasDiedBefore = true;
        OnDeathInLongBoat?.Invoke();
        Invoke("RestetDeath" , 5);
    }


    private void RestetDeath()
    {
        _hasDiedBefore = false;
    }
}
