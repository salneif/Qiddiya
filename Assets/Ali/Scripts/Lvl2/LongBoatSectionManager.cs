using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class LongBoatSectionManager : MonoBehaviour
{
    [SerializeField] private float timeToTurnAround;
    [SerializeField] private float _currentTime;
    [SerializeField] private int CurrentTurnNum = 0;
    

    public event Action<int> OnturnAround;

    private int _currentWoodBlockNum;

    private bool _inDangerZone = false;
    //ref
    [SerializeField] private A_BoatLever a_BoatLever;


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
    }
    private void OnDisable()
    {
        a_BoatLever.OnLeverSwitch -= OnLeverSwitch;
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
                    OnturnAround?.Invoke(CurrentTurnNum);
                }
                else if(CurrentTurnNum == 1)
                {
                    CurrentTurnNum = 0;
                    OnturnAround?.Invoke(CurrentTurnNum);

                }
                _currentTime = timeToTurnAround;
            }
        }
        switch (_currentWoodBlockNum)
        {
            case -1:
                // invoke death
                break;

                case 0:
                if(CurrentTurnNum != 0)
                {
                    // invoke death
                }
                break;

                case 1:
                if(CurrentTurnNum != 1)
                {
                    // invoke death
                }
                break;
        }

        
    }
}
