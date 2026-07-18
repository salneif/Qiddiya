using UnityEngine;

public class A_PlayerDeath_WaterSection : MonoBehaviour
{

   [SerializeField] private Animator animator;
    private PlayerController playerController;



    private void Start()
    {
        playerController = PlayerController.instance;
    }
    private void OnEnable()
    {
        A_WaterSection.OnEnemySeeingPlayer += OnEnemySeeingPlayer;
    }
    private void OnDisable()
    {
        A_WaterSection.OnEnemySeeingPlayer -= OnEnemySeeingPlayer;
    }

    private void OnEnemySeeingPlayer()
    {
        playerController.CanMove = false;
        animator.SetTrigger("Death");
        animator.SetBool("InAir" , false);
    }
}
