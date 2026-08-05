using UnityEngine;

public class A_MovingPlatform : MonoBehaviour
{

    private bool _isOnPlatform;

    [SerializeField] private Transform platform;
    [SerializeField] private Vector3 lastPlatformPostion;
    [SerializeField] private CharacterController characterController;

    //ref
    [SerializeField] private A_PlayerDeath_WaterSection playerDeath_WaterSection;

    private void Start()
    {
    }
    private void OnEnable()
    {
        playerDeath_WaterSection.OnPlayerDeath += WaterSection_OnPlayerDeath;
    }
    private void OnDisable()
    {
        playerDeath_WaterSection.OnPlayerDeath -= WaterSection_OnPlayerDeath;
    }

    private void WaterSection_OnPlayerDeath()
    {
        _isOnPlatform = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
           _isOnPlatform = true;
            lastPlatformPostion = platform.position;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            _isOnPlatform = false;
        }
    }

    private void Update()
    {

        Vector3 platformMovement = Vector3.zero;
        if (_isOnPlatform)
        {
            platformMovement = platform.position - lastPlatformPostion;
            lastPlatformPostion = platform.position;
        }
        if(characterController.enabled == true) 
            characterController.Move(platformMovement );
        
    }
}
