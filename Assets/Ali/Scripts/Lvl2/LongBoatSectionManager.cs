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
        _currentTime = timeToTurnAround;
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
    }

    private void OnLeverSwitch(int currentWoodBlockNum)
    {
        _currentWoodBlockNum = currentWoodBlockNum;
    }

    private void Update()
    {
        if(_currentTime > 0)
        {
            _currentTime -= Time.deltaTime;
            if(_currentTime <= 0)
            {
                if (CurrentTurnNum == 0)
                {
                    CurrentTurnNum = 1;
                    OnturnAround?.Invoke(1);
                    _inCahngeState = true;
                    Invoke("HandleActiveDeathFromZero", 5f);
                }
                else if(CurrentTurnNum == 1)
                {
                    CurrentTurnNum = 0;
                    OnturnAround?.Invoke(0);
                    _inCahngeState = true;
                    Invoke("HandleActiveDeathFromZero", 5f);


                }
                _currentTime = timeToTurnAround;
            }
        }
        if (_inDangerZone)
        {
            switch (_currentWoodBlockNum)
            {

                case -1:
                    
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
                    if (CurrentTurnNum != 1 &&  !_inCahngeState)
                    {
                        ActiveDeath();
                        Debug.Log("die1");

                    }
                    break;
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
