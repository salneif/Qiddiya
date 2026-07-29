using System;
using UnityEngine;

public class A_PlayerDeath_WaterSection : MonoBehaviour
{

   [SerializeField] private Animator animator;
    private PlayerController playerController;
    [SerializeField] private Material deathMatrial;
    [SerializeField] private Material OringanlMatrerial;
    [SerializeField] private GameObject mesh;

    private Renderer rend;


    // ref for death triggers 
   // [SerializeField] private A_WaterDeathZone waterDeathZone;
    [SerializeField] private ParticleSystem waterSplash;
    [SerializeField] private LongBoatSectionManager longBoatSectionManager;
    public  event Action OnPlayerFall;
    public event Action OnPlayerDeath;

   

    private void Awake()
    {
        
    }
    private void Start()
    {
        playerController = PlayerController.instance;
    }
    private void OnEnable()
    {
        // we subscrip to every event that kill the player in 
        //#1
        A_WaterSection.OnEnemySeeingPlayer += OnEnemySeeingPlayer;

        //#2
        A_WaterDeathZone.OnPlayerDrowning += OnPlayerDrowning;

        //#3

        longBoatSectionManager.OnDeathInLongBoat += OnDeathInLongBoat;
    }

   
    private void OnDisable()
    {
        // we unsub here so we dont have boom boom cpu 
        A_WaterSection.OnEnemySeeingPlayer -= OnEnemySeeingPlayer;
        A_WaterDeathZone.OnPlayerDrowning -= OnPlayerDrowning;
        longBoatSectionManager.OnDeathInLongBoat -= OnDeathInLongBoat;
    }
    private void OnDeathInLongBoat()
    {
        // death material
        rend = mesh.GetComponent<Renderer>();
        rend.material = deathMatrial;

        // stop moving
        animator.SetBool("InAir", false);
        playerController.CanMove = false;


        animator.SetTrigger("Death");

        Invoke("StartPlayerDeathSystem", 5);
    }

    private void OnPlayerDrowning()
    {
        // death material
        rend = mesh.GetComponent<Renderer>();
        rend.material = deathMatrial;


       // waterSplash.Play();
        Instantiate(waterSplash, transform.position ,transform.rotation);
        OnPlayerFall?.Invoke();


        // stop moving
        animator.SetBool("InAir", false);
        playerController.CanMove = false;


        animator.SetTrigger("Death");

        
        StartPlayerDeathSystem();
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

        Invoke("StartPlayerDeathSystem", 5);
        
    }
    private void StartPlayerDeathSystem()
    {
        OnPlayerDeath?.Invoke();
        rend.material = OringanlMatrerial;
    }
    private void Update()
    {
       
    }
}
