using Unity.VisualScripting;
using UnityEngine;

public class A_CheckPoint : MonoBehaviour
{
    [Header("CheckPoint")]
    [SerializeField] private Transform checkPoint1;
    [SerializeField] private Transform checkPoint2;
    [SerializeField] private Transform checkPoint3;

    [Header("Ref")]
    [SerializeField] private A_PlayerDeath_WaterSection playerDeath;
    [SerializeField] private GameObject player;
    [SerializeField] private Animator animator;
   


    private Transform _currentCheckPoint;
    private Transform playerTransform;
    private CharacterController characterController;
    private PlayerController playerController;


    private void Start()
    {
        playerTransform = player.transform;
        characterController = player.GetComponent<CharacterController>();
        playerController = player.GetComponent<PlayerController>();
    }
    private void Update()
    {
        _currentCheckPoint = checkPoint1;
    }
    private void OnEnable()
    {
        playerDeath.OnPlayerDeath += OnPlayerDeath;
    }
    private void OnDisable()
    {
        playerDeath.OnPlayerDeath -= OnPlayerDeath;
    }

    
    
    private void OnPlayerDeath()
    {
        characterController.enabled = false;

        animator.SetTrigger("ReActivePlayer");
        Invoke("AfterPlayerDeath", 2);
    }

    private void AfterPlayerDeath()
    {
        playerTransform.position = _currentCheckPoint.transform.position;

        characterController.enabled = true;
        playerController.CanMove = true;

        animator.ResetTrigger("ReActivePlayer");
    }
}
