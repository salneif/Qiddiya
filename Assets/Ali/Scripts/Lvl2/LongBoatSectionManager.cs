using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LongBoatSectionManager : MonoBehaviour
{
    [SerializeField] private float timeToTurnAround;
    [SerializeField] private float _currentTime;
    [SerializeField] private int CurrentTurnNum = 0;

    public event Action<int> OnturnAround;


    private void Awake()
    {
        _currentTime = timeToTurnAround;
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
        
    }
}
