using UnityEngine;

public class A_PlayerDeath_WaterSection : MonoBehaviour
{

   [SerializeField] private Animator animator;
    private PlayerController playerController;
    [SerializeField] private Material deathMatrial;
    [SerializeField] private GameObject mesh;

    private Renderer rend;


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
        // death material
         rend = mesh.GetComponent<Renderer>();
        rend.material = deathMatrial;

        // stop moving
        animator.SetBool("InAir", false);
        playerController.CanMove = false;


        animator.SetTrigger("Death");
        
    }


    private void Update()
    {
       
    }
}
