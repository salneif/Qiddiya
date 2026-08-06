using System;
using Unity.VisualScripting;
using UnityEngine;

public class A_CheckPointSystem : MonoBehaviour
{
    [Header("CheckPoint")]
    [SerializeField] private Transform checkPoint1;
    [SerializeField] private Transform checkPoint2;
    [SerializeField] private Transform checkPoint3;
    [SerializeField] private Transform checkPoint4;
    [SerializeField] private Transform checkPoint5;
    [SerializeField] private Transform checkPoint6;


    [Header("Ref")]
    [SerializeField] private A_PlayerDeath_WaterSection playerDeath;
    [SerializeField] private GameObject player;
    [SerializeField] private Animator animator;
   


    private Transform _currentCheckPoint;
    private Transform playerTransform;
    private CharacterController characterController;
    private PlayerController playerController;


    public event Action OnFadeStar;

    private void Start()
    {
        playerTransform = player.transform;
        characterController = player.GetComponent<CharacterController>();
        playerController = player.GetComponent<PlayerController>();
        _currentCheckPoint = checkPoint1;

    }
    private void Update()
    {
    }
    private void OnEnable()
    {
        playerDeath.OnPlayerDeath += OnPlayerDeath;
        A_CheckPoint.OnCheckPoint += OnCheckPoint;
    }

    private void OnCheckPoint(int checkPointNum)
    {
        switch(checkPointNum)
        {
            case 1:
                _currentCheckPoint = checkPoint1;
                break;
                case 2:
                _currentCheckPoint = checkPoint2;
                break;
                case 3:
                _currentCheckPoint = checkPoint3;
                break;
                case 4:
                _currentCheckPoint = checkPoint4;
                break;
                case 5:
                _currentCheckPoint = checkPoint5;
                break;
            case 6:
                _currentCheckPoint = checkPoint6;
                break;


        }
    }

    private void OnDisable()
    {
        playerDeath.OnPlayerDeath -= OnPlayerDeath;
        A_CheckPoint.OnCheckPoint -= OnCheckPoint;
    }

    
    
    private void OnPlayerDeath()
    {
        characterController.enabled = false;

        animator.SetTrigger("ReActivePlayer");
        Invoke("AfterPlayerDeath", 2);

        OnFadeStar?.Invoke();
    }

    private void AfterPlayerDeath()
    {
        playerTransform.position = _currentCheckPoint.transform.position;

        characterController.enabled = true;
        playerController.CanMove = true;

        animator.ResetTrigger("ReActivePlayer");
    }
}
